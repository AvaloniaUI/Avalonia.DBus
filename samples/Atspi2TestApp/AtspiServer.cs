using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.DBus.AutoGen;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AtspiServer
{
    private const int WindowToggleIntervalMs = 3000;

    internal static readonly (uint, (string, ObjectPath)[])[] s_emptyRelations = Array.Empty<(uint, (string, ObjectPath)[])>();
    private static readonly MessageValueReader<(string bus, string @event, string[] properties)> s_registryRegisteredReader = ReadRegistryRegistered;
    private static readonly MessageValueReader<(string bus, string @event)> s_registryDeregisteredReader = ReadRegistryDeregistered;

    private readonly AtspiTree _tree;
    private readonly Dictionary<int, string> _roleNames = new();
    private readonly object _treeGate = new();
    private readonly Dictionary<string, NodeHandlers> _handlersByPath = new(StringComparer.Ordinal);
    private readonly PathTree _pathTree;
    private readonly object _eventGate = new();
    private readonly HashSet<string> _registeredEvents = new(StringComparer.Ordinal);

    private Connection? _a11yConnection;
    private string _a11yAddress = string.Empty;
    private string _uniqueName = string.Empty;
    private bool _running;
    private int _windowToggleCounter;
    private volatile bool _emitObjectEvents;
    private CacheHandler? _cacheHandler;
    private OrgA11yAtspiRegistryProxy? _registryProxy;
    private IDisposable? _registryRegisteredSubscription;
    private IDisposable? _registryDeregisteredSubscription;
    private readonly System.Threading.ManualResetEventSlim _shutdownEvent = new(false);
    private System.Threading.Timer? _toggleTimer;
    private PosixSignalRegistration? _sigintRegistration;
    private PosixSignalRegistration? _sigtermRegistration;
    private System.Threading.Timer? _forceExitTimer;

    public AtspiServer(AtspiTree tree)
    {
        _tree = tree;
        _pathTree = new PathTree("/");
        _roleNames[RoleApplication] = "application";
        _roleNames[RoleFrame] = "frame";
        _roleNames[RoleLabel] = "label";
        _roleNames[RoleEntry] = "entry";
        _roleNames[RoleCheckBox] = "check box";
        _roleNames[RoleButton] = "button";
        _roleNames[RoleSlider] = "slider";
    }

    internal Connection A11yConnection => _a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");
    internal AtspiTree Tree => _tree;
    internal object TreeGate => _treeGate;

    public int Run()
    {
        _running = true;
        LogVerbose("Server starting");

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            RequestShutdown();
        };

        LogVerbose("Registering signal handlers");
        RegisterSignalHandlers();

        LogVerbose("Connecting to accessibility bus");
        if (!TryConnect())
        {
            Cleanup();
            return 1;
        }

        BuildHandlers();

        LogVerbose("Registering object paths");
        if (!RegisterObjectPaths())
        {
            Cleanup();
            return 1;
        }

        LogVerbose("Embedding application");
        if (!EmbedApplication())
        {
            Cleanup();
            return 1;
        }

        InitializeRegistryEventTracking();
        EmitInitialCacheSnapshot();

        StartToggleLoop();
        Console.WriteLine($"AT-SPI2 test app registered on {_uniqueName}");
        Console.WriteLine("Press Ctrl+C to exit.");
        LogVerbose("Waiting for shutdown");

        _shutdownEvent.Wait();

        Cleanup();
        return 0;
    }

    private void BuildHandlers()
    {
        _handlersByPath.Clear();
        foreach (var node in _tree.NodesByPath.Values)
        {
            var handlers = new NodeHandlers(node, _pathTree.AddPath(node.Path));

            if (node.Interfaces.Contains(IfaceAccessible))
            {
                var handler = new AccessibleHandler(this, node);
                handlers.AccessibleHandler = handler;
                handlers.Add(handler);
            }

            if (node.Interfaces.Contains(IfaceApplication))
            {
                var handler = new ApplicationHandler(this, node);
                handlers.ApplicationHandler = handler;
                handlers.Add(handler);
            }

            if (node.Interfaces.Contains(IfaceComponent))
            {
                var handler = new ComponentHandler(this, node);
                handlers.ComponentHandler = handler;
                handlers.Add(handler);
            }

            if (node.Interfaces.Contains(IfaceAction))
            {
                var handler = new ActionHandler(this, node);
                handlers.ActionHandler = handler;
                handlers.Add(handler);
            }

            if (node.Interfaces.Contains(IfaceValue))
            {
                var handler = new ValueHandler(this, node);
                handlers.ValueHandler = handler;
                handlers.Add(handler);
            }

            handlers.EventObjectHandler = new EventObjectHandler(this);
            handlers.Add(handlers.EventObjectHandler);

            _handlersByPath[node.Path] = handlers;
        }

        var cachePathHandler = _pathTree.AddPath(CachePath);
        var cacheHandler = new CacheHandler(this);
        cachePathHandler.Add(cacheHandler);
        _cacheHandler = cacheHandler;

        RefreshAllAccessibleHandlers();
    }

    private void RefreshAllAccessibleHandlers()
    {
        foreach (var handler in _handlersByPath.Values)
        {
            handler.AccessibleHandler?.RefreshProperties();
        }
    }

    private void RefreshAccessibleHandler(AccessibleNode node)
    {
        if (_handlersByPath.TryGetValue(node.Path, out var handler))
        {
            handler.AccessibleHandler?.RefreshProperties();
        }
    }

    private bool TryConnect()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("TryConnect start");
        var address = GetAccessibilityBusAddress();
        if (string.IsNullOrWhiteSpace(address))
        {
            Console.Error.WriteLine("Failed to resolve the accessibility bus address.");
            return false;
        }
        _a11yAddress = address;

        try
        {
            LogVerbose("Opening private accessibility bus connection");
            _a11yConnection = new Connection(address);
            _uniqueName = _a11yConnection.UniqueName;
            LogVerbose($"TryConnect end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to open accessibility bus: {ex.Message}");
            LogVerbose($"TryConnect failed after {sw.ElapsedMilliseconds} ms");
            _a11yConnection = null;
            return false;
        }
    }

    private string GetAccessibilityBusAddress()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("GetAddress start");
        try
        {
            LogVerbose("Connecting to session bus for org.a11y.Bus");
            using var connection = new Connection(DBusBusType.DBUS_BUS_SESSION);
            var proxy = new OrgA11yBusProxy(connection, BusNameA11y, PathA11y);
            LogVerbose("Calling org.a11y.Bus.GetAddress");
            return proxy.GetAddressAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GetAddress call failed: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            LogVerbose($"GetAddress end ({sw.ElapsedMilliseconds} ms)");
        }
    }

    private bool RegisterObjectPaths()
    {
        var sw = Stopwatch.StartNew();
        if (_a11yConnection == null)
        {
            Console.Error.WriteLine("Missing accessibility bus connection.");
            return false;
        }

        try
        {
            _a11yConnection.RegisterPathHandler(_pathTree);
            LogVerbose($"RegisterObjectPaths end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to register object path fallback: {ex.Message}");
            return false;
        }
    }

    private bool EmbedApplication()
    {
        if (string.IsNullOrWhiteSpace(_a11yAddress))
        {
            Console.Error.WriteLine("Missing accessibility bus address.");
            return false;
        }
        if (_a11yConnection == null)
        {
            Console.Error.WriteLine("Missing accessibility bus connection.");
            return false;
        }

        var sw = Stopwatch.StartNew();
        LogVerbose("EmbedApplication start");
        try
        {
            var proxy = new OrgA11yAtspiSocketProxy(_a11yConnection, BusNameRegistry, RootPath);
            LogVerbose("Calling org.a11y.atspi.Socket.Embed");
            var reply = proxy.EmbedAsync((_uniqueName, new ObjectPath(RootPath))).GetAwaiter().GetResult();
            Console.WriteLine($"Registry root: {reply.Item1} {reply.Item2}");
            LogVerbose($"EmbedApplication end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Embed call failed: {ex.Message}");
            LogVerbose($"EmbedApplication failed after {sw.ElapsedMilliseconds} ms");
            return false;
        }
    }

    private void InitializeRegistryEventTracking()
    {
        if (_a11yConnection == null)
        {
            return;
        }

        try
        {
            _registryProxy ??= new OrgA11yAtspiRegistryProxy(_a11yConnection, BusNameRegistry, RegistryPath);
            var events = _registryProxy.GetRegisteredEventsAsync().GetAwaiter().GetResult();
            lock (_eventGate)
            {
                _registeredEvents.Clear();
                foreach (var registered in events)
                {
                    _registeredEvents.Add(registered.Item2);
                }
                UpdateEventMaskLocked();
            }

            var registeredRule = new MatchRule
            {
                Type = MessageType.Signal,
                Path = RegistryPath,
                Interface = "org.a11y.atspi.Registry",
                Member = "EventListenerRegistered"
            };
            _registryRegisteredSubscription = _a11yConnection
                .AddMatchAsync(registeredRule, s_registryRegisteredReader,
                    (error, args, _) => OnRegistryEventListenerRegistered(error, args))
                .GetAwaiter()
                .GetResult();

            var deregisteredRule = new MatchRule
            {
                Type = MessageType.Signal,
                Path = RegistryPath,
                Interface = "org.a11y.atspi.Registry",
                Member = "EventListenerDeregistered"
            };
            _registryDeregisteredSubscription = _a11yConnection
                .AddMatchAsync(deregisteredRule, s_registryDeregisteredReader,
                    (error, args, _) => OnRegistryEventListenerDeregistered(error, args))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            LogVerbose($"Registry event tracking unavailable: {ex.Message}");
            _emitObjectEvents = true;
        }
    }

    private void OnRegistryEventListenerRegistered(Exception? error, (string bus, string @event, string[] properties) args)
    {
        if (error != null)
        {
            LogVerbose($"Registry EventListenerRegistered error: {error.Message}");
            return;
        }

        lock (_eventGate)
        {
            _registeredEvents.Add(args.@event);
            UpdateEventMaskLocked();
        }
    }

    private void OnRegistryEventListenerDeregistered(Exception? error, (string bus, string @event) args)
    {
        if (error != null)
        {
            LogVerbose($"Registry EventListenerDeregistered error: {error.Message}");
            return;
        }

        lock (_eventGate)
        {
            _registeredEvents.Remove(args.@event);
            UpdateEventMaskLocked();
        }
    }

    private void UpdateEventMaskLocked()
    {
        _emitObjectEvents = _registeredEvents.Any(IsObjectEventClass);
    }

    private static bool IsObjectEventClass(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        if (eventName == "*")
        {
            return true;
        }

        return eventName.StartsWith("object:", StringComparison.OrdinalIgnoreCase)
            || eventName.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
            || eventName.StartsWith("focus:", StringComparison.OrdinalIgnoreCase);
    }

    private static (string bus, string @event, string[] properties) ReadRegistryRegistered(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        var bus = reader.ReadString();
        var eventName = reader.ReadString();
        var properties = ReadStringArray(ref reader);
        return (bus, eventName, properties);
    }

    private static (string bus, string @event) ReadRegistryDeregistered(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        var bus = reader.ReadString();
        var eventName = reader.ReadString();
        return (bus, eventName);
    }

    private static string[] ReadStringArray(ref Reader reader)
    {
        var end = reader.ReadArrayStart();
        var items = new List<string>();
        while (reader.HasNext(end))
        {
            items.Add(reader.ReadString());
        }
        return items.ToArray();
    }

    private void EmitInitialCacheSnapshot()
    {
        if (_cacheHandler == null)
        {
            return;
        }

        AccessibleNode[] snapshot;
        lock (_treeGate)
        {
            snapshot = _tree.NodesByPath.Values
                .OrderBy(static node => node.Path, StringComparer.Ordinal)
                .ToArray();
        }

        foreach (var node in snapshot)
        {
            EmitCacheAdd(node);
        }
    }

    private void EmitCacheAdd(AccessibleNode node)
    {
        if (_cacheHandler == null || _a11yConnection == null)
        {
            return;
        }

        var item = BuildCacheItem(node);
        _cacheHandler.EmitAddAccessibleSignal(item);
    }

    private void EmitCacheRemove(AccessibleNode node)
    {
        if (_cacheHandler == null || _a11yConnection == null)
        {
            return;
        }

        _cacheHandler.EmitRemoveAccessibleSignal(GetReference(node));
    }

    private void EmitCacheAddSubtree(AccessibleNode node)
    {
        EmitCacheAdd(node);
        foreach (var child in node.Children)
        {
            EmitCacheAddSubtree(child);
        }
    }

    private void EmitCacheRemoveSubtree(AccessibleNode node)
    {
        foreach (var child in node.Children)
        {
            EmitCacheRemoveSubtree(child);
        }
        EmitCacheRemove(node);
    }

    internal ((string, ObjectPath), (string, ObjectPath), (string, ObjectPath), int, int, string[], string, uint, string, uint[])
        BuildCacheItem(AccessibleNode node)
    {
        var self = (_uniqueName, new ObjectPath(node.Path));
        var app = (_uniqueName, new ObjectPath(RootPath));
        var parent = node.Parent == null
            ? (string.Empty, new ObjectPath(NullPath))
            : (_uniqueName, new ObjectPath(node.Parent.Path));
        var indexInParent = node.Parent == null ? -1 : node.Parent.Children.IndexOf(node);
        var childCount = node.Children.Count;
        var interfaces = node.Interfaces.Count == 0
            ? Array.Empty<string>()
            : node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray();
        var name = node.Name;
        var role = (uint)node.Role;
        var description = node.Description;
        var states = BuildStateSet(node.States);

        return (self, app, parent, indexInParent, childCount, interfaces, name, role, description, states);
    }

    private void Cleanup()
    {
        if (_toggleTimer != null)
        {
            _toggleTimer.Dispose();
            _toggleTimer = null;
        }

        if (_a11yConnection != null)
        {
            _a11yConnection.Dispose();
            _a11yConnection = null;
        }

        _a11yAddress = string.Empty;
        _cacheHandler = null;
        _registryProxy = null;
        _registryRegisteredSubscription?.Dispose();
        _registryRegisteredSubscription = null;
        _registryDeregisteredSubscription?.Dispose();
        _registryDeregisteredSubscription = null;
        lock (_eventGate)
        {
            _registeredEvents.Clear();
            _emitObjectEvents = false;
        }

        _sigintRegistration?.Dispose();
        _sigintRegistration = null;
        _sigtermRegistration?.Dispose();
        _sigtermRegistration = null;
        _forceExitTimer?.Dispose();
        _forceExitTimer = null;
    }

    private void RegisterSignalHandlers()
    {
        _sigintRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            RequestShutdown();
        });
        _sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            RequestShutdown();
        });
    }

    private void RequestShutdown()
    {
        _running = false;
        _shutdownEvent.Set();
        _forceExitTimer ??= new System.Threading.Timer(
            _ => Environment.Exit(0),
            null,
            2000,
            System.Threading.Timeout.Infinite);
    }

    private void StartToggleLoop()
    {
        _toggleTimer = new System.Threading.Timer(
            _ => ToggleWindow(),
            null,
            WindowToggleIntervalMs,
            WindowToggleIntervalMs);
    }

    private void ToggleWindow()
    {
        if (!_running || _a11yConnection == null)
        {
            return;
        }

        lock (_treeGate)
        {
            if (_tree.IsToggleWindowAttached)
            {
                var index = _tree.GetToggleWindowIndex();
                _tree.RemoveToggleWindow();
                _pathTree.RemovePath(_tree.ToggleWindow.Path);
                RefreshAccessibleHandler(_tree.Root);
                RefreshAccessibleHandler(_tree.ToggleWindow);
                EmitChildrenChanged(_tree.Root, "remove", index < 0 ? 0 : index, _tree.ToggleWindow);
                EmitCacheRemoveSubtree(_tree.ToggleWindow);
            }
            else
            {
                _windowToggleCounter++;
                _tree.ToggleWindow.Description = $"Recurring window (cycle {_windowToggleCounter})";
                _tree.AddToggleWindow();
                _pathTree.AddPath(_tree.ToggleWindow.Path);
                RefreshAccessibleHandler(_tree.Root);
                RefreshAccessibleHandler(_tree.ToggleWindow);
                var index = _tree.GetToggleWindowIndex();
                EmitChildrenChanged(_tree.Root, "add", index < 0 ? 0 : index, _tree.ToggleWindow);
                EmitPropertyChange(_tree.ToggleWindow, "accessible-description", _tree.ToggleWindow.Description);
                EmitCacheAddSubtree(_tree.ToggleWindow);
            }
        }
    }

    internal (string busName, ObjectPath path) GetReference(AccessibleNode? node)
    {
        if (node == null)
        {
            return (string.Empty, new ObjectPath(NullPath));
        }

        return (_uniqueName, new ObjectPath(node.Path));
    }

    private void EmitChildrenChanged(AccessibleNode parent, string operation, int index, AccessibleNode child)
    {
        if (_a11yConnection == null)
        {
            return;
        }
        if (!_emitObjectEvents)
        {
            return;
        }

        if (!_handlersByPath.TryGetValue(parent.Path, out var handlers) || handlers.EventObjectHandler == null)
        {
            return;
        }

        var reference = GetReference(child);
        var childVariant = VariantValue.FromSignature("(so)", (reference.busName, reference.path));
        handlers.EventObjectHandler.EmitChildrenChangedSignal(operation, index, childVariant);
    }

    private void EmitPropertyChange(AccessibleNode node, string propertyName, string value)
    {
        if (_a11yConnection == null)
        {
            return;
        }
        if (!_emitObjectEvents)
        {
            return;
        }

        if (!_handlersByPath.TryGetValue(node.Path, out var handlers) || handlers.EventObjectHandler == null)
        {
            return;
        }

        handlers.EventObjectHandler.EmitPropertyChangeSignal(propertyName, VariantValue.FromSignature("s", value));
    }

    internal void SetFocused(AccessibleNode node)
    {
        foreach (var item in _tree.NodesByPath.Values)
        {
            item.States.Remove(StateFocused);
        }

        node.States.Add(StateFocused);
    }

    internal bool ContainsPoint(Rect rect, int x, int y)
    {
        return x >= rect.X && y >= rect.Y && x < rect.X + rect.Width && y < rect.Y + rect.Height;
    }

    internal (int x, int y) TranslatePoint(AccessibleNode node, int x, int y, uint coordType)
    {
        if (coordType == 0)
        {
            return (x, y);
        }

        if (coordType == 1)
        {
            var topLevel = GetTopLevelWindow(node);
            if (topLevel != null)
            {
                return (x + topLevel.Extents.X, y + topLevel.Extents.Y);
            }
        }

        if (coordType == 2 && node.Parent != null)
        {
            return (x + node.Parent.Extents.X, y + node.Parent.Extents.Y);
        }

        return (x, y);
    }

    internal Rect TranslateRect(AccessibleNode node, uint coordType)
    {
        var rect = node.Extents;
        if (coordType == 0)
        {
            return rect;
        }

        if (coordType == 1)
        {
            var topLevel = GetTopLevelWindow(node);
            if (topLevel != null)
            {
                return new Rect(rect.X - topLevel.Extents.X, rect.Y - topLevel.Extents.Y, rect.Width, rect.Height);
            }
        }

        if (coordType == 2 && node.Parent != null)
        {
            return new Rect(rect.X - node.Parent.Extents.X, rect.Y - node.Parent.Extents.Y, rect.Width, rect.Height);
        }

        return rect;
    }

    internal AccessibleNode? FindAtPoint(AccessibleNode node, int x, int y)
    {
        foreach (var child in node.Children)
        {
            if (ContainsPoint(child.Extents, x, y))
            {
                return FindAtPoint(child, x, y) ?? child;
            }
        }

        return ContainsPoint(node.Extents, x, y) ? node : null;
    }

    private AccessibleNode? GetTopLevelWindow(AccessibleNode node)
    {
        var current = node;
        while (current.Parent != null && current.Parent != _tree.Root)
        {
            current = current.Parent;
        }

        return current.Parent == _tree.Root ? current : null;
    }

    internal string GetRoleName(int role)
    {
        return _roleNames.TryGetValue(role, out var name) ? name : "unknown";
    }
}
