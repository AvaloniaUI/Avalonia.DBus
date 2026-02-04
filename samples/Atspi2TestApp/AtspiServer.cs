using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AtspiServer
{
    private const int WindowToggleIntervalMs = 3000;
    private const int StartupRetryDelayMs = 2000;
    private const int StartupRetryMaxDelayMs = 15000;

    internal static readonly DBusArray<DBusStruct> s_emptyRelations = new("(ua(so))");

    private readonly AtspiTree _tree;
    private readonly Dictionary<int, string> _roleNames = new();
    private readonly object _treeGate = new();
    private readonly Dictionary<string, NodeHandlers> _handlersByPath = new(StringComparer.Ordinal);
    private readonly PathTree _pathTree;
    private readonly object _eventGate = new();
    private readonly HashSet<string> _registeredEvents = new(StringComparer.Ordinal);
    private readonly object _registryGate = new();
    private readonly System.Threading.SemaphoreSlim _registrySubscriptionGate = new(1, 1);

    private DBusConnection? _a11yConnection;
    private string _a11yAddress = string.Empty;
    private string _uniqueName = string.Empty;
    private string? _registryUniqueName;
    private bool _running;
    private int _windowToggleCounter;
    private volatile bool _emitObjectEvents;
    private CacheHandler? _cacheHandler;
    private OrgA11yAtspiRegistryProxy? _registryProxy;
    private IDisposable? _pathTreeRegistration;
    private IDisposable? _registryRegisteredSubscription;
    private IDisposable? _registryDeregisteredSubscription;
    private IDisposable? _registryOwnerChangedSubscription;
    private readonly System.Threading.Tasks.TaskCompletionSource<bool> _shutdownTcs = new(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
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

    internal DBusConnection A11yConnection => _a11yConnection ?? throw new InvalidOperationException("Connection not initialized.");
    internal AtspiTree Tree => _tree;
    internal object TreeGate => _treeGate;

    public async System.Threading.Tasks.Task<int> RunAsync()
    {
        _running = true;
        LogVerbose("Server starting");

        Console.CancelKeyPress += OnCancelKeyPress;
        LogVerbose("Registering signal handlers");
        RegisterSignalHandlers();

        var attempt = 0;
        var started = false;

        while (_running)
        {
            attempt++;
            if (await TryStartAsync())
            {
                started = true;
                LogVerbose("Waiting for shutdown");
                await _shutdownTcs.Task;
                break;
            }

            await CleanupAttemptAsync();

            if (!_running || _shutdownTcs.Task.IsCompleted)
            {
                break;
            }

            var delay = ComputeRetryDelay(attempt);
            Console.Error.WriteLine($"Startup failed; retrying in {(int)delay.TotalSeconds}s.");
            var completed = await System.Threading.Tasks.Task.WhenAny(_shutdownTcs.Task, System.Threading.Tasks.Task.Delay(delay));
            if (completed == _shutdownTcs.Task)
            {
                break;
            }
        }

        await CleanupAsync();
        return started ? 0 : 1;
    }

    private async System.Threading.Tasks.Task<bool> TryStartAsync()
    {
        LogVerbose("Connecting to accessibility bus");
        if (!await TryConnectAsync())
        {
            return false;
        }

        BuildHandlers();

        LogVerbose("Registering object paths");
        if (!RegisterObjectPaths())
        {
            return false;
        }

        LogVerbose("Embedding application");
        if (!await EmbedApplicationAsync())
        {
            return false;
        }

        await InitializeRegistryEventTrackingAsync();
        EmitInitialCacheSnapshot();

        StartToggleLoop();
        Console.WriteLine($"AT-SPI2 test app registered on {_uniqueName}");
        Console.WriteLine("Press Ctrl+C to exit.");
        return true;
    }

    private static TimeSpan ComputeRetryDelay(int attempt)
    {
        var factor = attempt < 1 ? 1 : attempt;
        var delayMs = StartupRetryDelayMs * factor;
        if (delayMs > StartupRetryMaxDelayMs)
        {
            delayMs = StartupRetryMaxDelayMs;
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private void BuildHandlers()
    {
        _handlersByPath.Clear();
        foreach (var node in _tree.NodesByPath.Values)
        {
            var pathHandler = _pathTree.AddPath(node.Path);
            pathHandler.Clear();
            var handlers = new NodeHandlers(node, pathHandler);

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
        cachePathHandler.Clear();
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

    private async System.Threading.Tasks.Task<bool> TryConnectAsync()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("TryConnect start");
        var address = await GetAccessibilityBusAddressAsync();
        if (string.IsNullOrWhiteSpace(address))
        {
            Console.Error.WriteLine("Failed to resolve the accessibility bus address.");
            return false;
        }
        _a11yAddress = address;

        try
        {
            LogVerbose("Opening private accessibility bus connection");
            _a11yConnection = await DBusConnection.ConnectAsync(address);
            _uniqueName = await _a11yConnection.GetUniqueNameAsync() ?? "";
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

    private async System.Threading.Tasks.Task<string> GetAccessibilityBusAddressAsync()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("GetAddress start");
        try
        {
            LogVerbose("Connecting to session bus for org.a11y.Bus");
            await using var connection = await DBusConnection.ConnectSessionAsync();
            var proxy = new OrgA11yBusProxy(connection, BusNameA11y, new DBusObjectPath(PathA11y));
            LogVerbose("Calling org.a11y.Bus.GetAddress");
            return await proxy.GetAddressAsync();
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
            RefreshPathRegistrations();
            LogVerbose($"RegisterObjectPaths end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to register object path fallback: {ex.Message}");
            return false;
        }
    }

    private void RefreshPathRegistrations()
    {
        if (_a11yConnection == null)
        {
            return;
        }

        _pathTreeRegistration?.Dispose();
        _pathTreeRegistration = _pathTree.Register(_a11yConnection);
    }

    private async System.Threading.Tasks.Task<bool> EmbedApplicationAsync()
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
            var proxy = new OrgA11yAtspiSocketProxy(_a11yConnection, BusNameRegistry, new DBusObjectPath(RootPath));
            LogVerbose("Calling org.a11y.atspi.Socket.Embed");
            var reply = await proxy.EmbedAsync(new DBusStruct(_uniqueName, new DBusObjectPath(RootPath)));
            var registryBus = reply.Count > 0 ? reply[0] : null;
            var registryPath = reply.Count > 1 ? reply[1] : null;
            Console.WriteLine($"Registry root: {registryBus} {registryPath}");
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

    private async System.Threading.Tasks.Task InitializeRegistryEventTrackingAsync()
    {
        if (_a11yConnection == null)
        {
            return;
        }

        try
        {
            _registryProxy ??= new OrgA11yAtspiRegistryProxy(_a11yConnection, BusNameRegistry, new DBusObjectPath(RegistryPath));
            var events = await _registryProxy.GetRegisteredEventsAsync();
            lock (_eventGate)
            {
                _registeredEvents.Clear();
                foreach (var registered in events)
                {
                    if (registered.Count > 1 && registered[1] is string eventName)
                    {
                        _registeredEvents.Add(eventName);
                    }
                }
                UpdateEventMaskLocked();
            }

            var registryOwner = await ResolveRegistryUniqueNameAsync();
            await UpdateRegistrySignalSubscriptionsAsync(registryOwner);

            _registryOwnerChangedSubscription ??= await _a11yConnection.SubscribeAsync(
                sender: null,
                path: new DBusObjectPath("/org/freedesktop/DBus"),
                iface: "org.freedesktop.DBus",
                member: "NameOwnerChanged",
                handler: message =>
                {
                    var name = (string)message.Body[0];
                    if (!string.Equals(name, BusNameRegistry, StringComparison.Ordinal))
                    {
                        return System.Threading.Tasks.Task.CompletedTask;
                    }

                    var newOwner = (string)message.Body[2];
                    var owner = string.IsNullOrWhiteSpace(newOwner) ? null : newOwner;
                    FireAndForget(UpdateRegistrySignalSubscriptionsAsync(owner));
                    return System.Threading.Tasks.Task.CompletedTask;
                },
                synchronizationContext: null);
        }
        catch (Exception ex)
        {
            LogVerbose($"Registry event tracking unavailable: {ex.Message}");
            _emitObjectEvents = true;
        }
    }

    private void OnRegistryEventListenerRegistered(string bus, string @event, DBusArray<string> properties)
    {
        lock (_eventGate)
        {
            _registeredEvents.Add(@event);
            UpdateEventMaskLocked();
        }
    }

    private void OnRegistryEventListenerDeregistered(string bus, string @event)
    {
        lock (_eventGate)
        {
            _registeredEvents.Remove(@event);
            UpdateEventMaskLocked();
        }
    }

    private void UpdateEventMaskLocked()
    {
        _emitObjectEvents = _registeredEvents.Any(IsObjectEventClass);
    }

    private async System.Threading.Tasks.Task<string?> ResolveRegistryUniqueNameAsync()
    {
        if (_a11yConnection == null)
        {
            return null;
        }

        try
        {
            var reply = await _a11yConnection.CallMethodAsync(
                "org.freedesktop.DBus",
                new DBusObjectPath("/org/freedesktop/DBus"),
                "org.freedesktop.DBus",
                "GetNameOwner",
                BusNameRegistry);

            if (reply.Body.Count > 0 && reply.Body[0] is string owner && !string.IsNullOrWhiteSpace(owner))
            {
                return owner;
            }
        }
        catch (Exception ex)
        {
            LogVerbose($"GetNameOwner failed: {ex.Message}");
        }

        return null;
    }

    private async System.Threading.Tasks.Task UpdateRegistrySignalSubscriptionsAsync(string? registryOwner)
    {
        if (_a11yConnection == null)
        {
            return;
        }

        await _registrySubscriptionGate.WaitAsync();
        try
        {
            IDisposable? oldRegistered;
            IDisposable? oldDeregistered;
            lock (_registryGate)
            {
                if (string.Equals(_registryUniqueName, registryOwner, StringComparison.Ordinal))
                {
                    return;
                }

                oldRegistered = _registryRegisteredSubscription;
                oldDeregistered = _registryDeregisteredSubscription;
                _registryRegisteredSubscription = null;
                _registryDeregisteredSubscription = null;
                _registryUniqueName = registryOwner;
            }

            oldRegistered?.Dispose();
            oldDeregistered?.Dispose();

            string? senderFilter = string.IsNullOrWhiteSpace(registryOwner) ? null : registryOwner;

            IDisposable? registered = null;
            IDisposable? deregistered = null;
            try
            {
                registered = await _a11yConnection.SubscribeAsync(
                    sender: senderFilter,
                    path: new DBusObjectPath(RegistryPath),
                    iface: "org.a11y.atspi.Registry",
                    member: "EventListenerRegistered",
                    handler: message =>
                    {
                        var bus = (string)message.Body[0];
                        var @event = (string)message.Body[1];
                        var properties = (DBusArray<string>)message.Body[2];
                        OnRegistryEventListenerRegistered(bus, @event, properties);
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    synchronizationContext: null);

                deregistered = await _a11yConnection.SubscribeAsync(
                    sender: senderFilter,
                    path: new DBusObjectPath(RegistryPath),
                    iface: "org.a11y.atspi.Registry",
                    member: "EventListenerDeregistered",
                    handler: message =>
                    {
                        var bus = (string)message.Body[0];
                        var @event = (string)message.Body[1];
                        OnRegistryEventListenerDeregistered(bus, @event);
                        return System.Threading.Tasks.Task.CompletedTask;
                    },
                    synchronizationContext: null);
            }
            catch
            {
                registered?.Dispose();
                deregistered?.Dispose();
                throw;
            }

            lock (_registryGate)
            {
                _registryRegisteredSubscription = registered;
                _registryDeregisteredSubscription = deregistered;
            }
        }
        finally
        {
            _registrySubscriptionGate.Release();
        }
    }

    private static void FireAndForget(System.Threading.Tasks.Task task)
    {
        if (task == null)
        {
            return;
        }

        _ = task.ContinueWith(
            t => _ = t.Exception,
            System.Threading.CancellationToken.None,
            System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted,
            System.Threading.Tasks.TaskScheduler.Default);
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

    internal DBusStruct BuildCacheItem(AccessibleNode node)
    {
        var self = new DBusStruct(_uniqueName, new DBusObjectPath(node.Path));
        var app = new DBusStruct(_uniqueName, new DBusObjectPath(RootPath));
        var parent = node.Parent == null
            ? new DBusStruct(string.Empty, new DBusObjectPath(NullPath))
            : new DBusStruct(_uniqueName, new DBusObjectPath(node.Parent.Path));
        var indexInParent = node.Parent == null ? -1 : node.Parent.Children.IndexOf(node);
        var childCount = node.Children.Count;
        var interfaces = node.Interfaces.Count == 0
            ? new DBusArray<string>()
            : new DBusArray<string>(node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray());
        var name = node.Name;
        var role = (uint)node.Role;
        var description = node.Description;
        var states = BuildStateSet(node.States);

        return new DBusStruct(self, app, parent, indexInParent, childCount, interfaces, name, role, description, states);
    }

    private async System.Threading.Tasks.Task CleanupAttemptAsync()
    {
        if (_toggleTimer != null)
        {
            _toggleTimer.Dispose();
            _toggleTimer = null;
        }

        _pathTreeRegistration?.Dispose();
        _pathTreeRegistration = null;

        if (_a11yConnection != null)
        {
            await _a11yConnection.DisposeAsync();
            _a11yConnection = null;
        }

        _a11yAddress = string.Empty;
        _uniqueName = string.Empty;
        _cacheHandler = null;
        _registryProxy = null;
        _registryRegisteredSubscription?.Dispose();
        _registryRegisteredSubscription = null;
        _registryDeregisteredSubscription?.Dispose();
        _registryDeregisteredSubscription = null;
        _registryOwnerChangedSubscription?.Dispose();
        _registryOwnerChangedSubscription = null;
        _registryUniqueName = null;
        lock (_eventGate)
        {
            _registeredEvents.Clear();
            _emitObjectEvents = false;
        }
    }

    private async System.Threading.Tasks.Task CleanupAsync()
    {
        await CleanupAttemptAsync();

        Console.CancelKeyPress -= OnCancelKeyPress;
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
        _shutdownTcs.TrySetResult(true);
        _forceExitTimer ??= new System.Threading.Timer(
            _ => Environment.Exit(0),
            null,
            2000,
            System.Threading.Timeout.Infinite);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        RequestShutdown();
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
                RefreshPathRegistrations();
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
                RefreshPathRegistrations();
                RefreshAccessibleHandler(_tree.Root);
                RefreshAccessibleHandler(_tree.ToggleWindow);
                var index = _tree.GetToggleWindowIndex();
                EmitChildrenChanged(_tree.Root, "add", index < 0 ? 0 : index, _tree.ToggleWindow);
                EmitPropertyChange(_tree.ToggleWindow, "accessible-description", _tree.ToggleWindow.Description);
                EmitCacheAddSubtree(_tree.ToggleWindow);
            }
        }
    }

    internal DBusStruct GetReference(AccessibleNode? node)
    {
        if (node == null)
        {
            return new DBusStruct(string.Empty, new DBusObjectPath(NullPath));
        }

        return new DBusStruct(_uniqueName, new DBusObjectPath(node.Path));
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
        var childVariant = new DBusVariant(reference);
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

        handlers.EventObjectHandler.EmitPropertyChangeSignal(propertyName, new DBusVariant(value));
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
