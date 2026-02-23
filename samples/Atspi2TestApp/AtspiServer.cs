using System.Diagnostics;
using System.Runtime.InteropServices;
using Atspi2TestApp.DBusXml;
using Avalonia.DBus;
using static Atspi2TestApp.Program;

namespace Atspi2TestApp;

internal sealed class AtspiServer
{
    private const int WindowToggleIntervalMs = 3000;
    private const int StartupRetryDelayMs = 2000;
    private const int StartupRetryMaxDelayMs = 15000;

    internal static readonly List<AtSpiRelationEntry> EmptyRelations = [];

    private readonly AtspiTree _tree;
    private readonly IDBusDiagnostics? _diagnostics;
    private readonly Dictionary<int, string> _roleNames = new();
    private readonly object _treeGate = new();
    private readonly object _eventGate = new();
    private readonly HashSet<string> _registeredEvents = new(StringComparer.Ordinal);
    private readonly object _registryGate = new();
    private readonly SemaphoreSlim _registrySubscriptionGate = new(1, 1);
    private readonly Dictionary<string, ActivePathRegistration> _pathRegistrations = new(StringComparer.Ordinal);

    private DBusConnection? _a11yConnection;
    private string _a11yAddress = string.Empty;
    private string _uniqueName = string.Empty;
    private string? _registryUniqueName;
    private bool _running;
    private int _windowToggleCounter;
    private volatile bool _emitObjectEvents;
    private CacheHandler? _cacheHandler;
    private OrgA11yAtspiRegistryProxy? _registryProxy;
    private IDisposable? _registryRegisteredSubscription;
    private IDisposable? _registryDeregisteredSubscription;
    private IDisposable? _registryOwnerChangedSubscription;
    private readonly TaskCompletionSource<bool> _shutdownTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Timer? _toggleTimer;
    private PosixSignalRegistration? _sigintRegistration;
    private PosixSignalRegistration? _sigtermRegistration;
    private Timer? _forceExitTimer;

    public AtspiServer(AtspiTree tree, IDBusDiagnostics? diagnostics = null)
    {
        _tree = tree;
        _diagnostics = diagnostics;
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

    public async Task<int> RunAsync()
    {
        _running = true;
        LogVerbose("Server starting");
        LogSampleFlags();

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
            await Console.Error.WriteLineAsync($"Startup failed; retrying in {(int)delay.TotalSeconds}s.");
            var completed = await Task.WhenAny(_shutdownTcs.Task, Task.Delay(delay));
            if (completed == _shutdownTcs.Task)
            {
                break;
            }
        }

        await CleanupAsync();
        return started ? 0 : 1;
    }

    private async Task<bool> TryStartAsync()
    {
        LogVerbose("Connecting to accessibility bus");
        if (!await TryConnectAsync())
        {
            return false;
        }

        BuildHandlers();

        LogVerbose("Registering object paths");
        if (!await RegisterObjectPathsAsync())
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
        foreach (var node in _tree.NodesByPath.Values)
        {
            var handlers = new NodeHandlers(node);

            if (node.Interfaces.Contains(IfaceAccessible))
            {
                var handler = new AccessibleHandler(this, node);
                handlers.AccessibleHandler = handler;
            }

            if (node.Interfaces.Contains(IfaceApplication))
            {
                var handler = new ApplicationHandler(this, node);
                handlers.ApplicationHandler = handler;
            }

            if (node.Interfaces.Contains(IfaceComponent))
            {
                var handler = new ComponentHandler(this, node);
                handlers.ComponentHandler = handler;
            }

            if (node.Interfaces.Contains(IfaceAction))
            {
                var handler = new ActionHandler(this, node);
                handlers.ActionHandler = handler;
            }

            if (node.Interfaces.Contains(IfaceValue))
            {
                var handler = new ValueHandler(this, node);
                handlers.ValueHandler = handler;
            }

            if (node.Interfaces.Contains(IfaceImage))
            {
                var handler = new ImageHandler(this, node);
                handlers.ImageHandler = handler;
            }

            handlers.EventObjectHandler = new EventObjectHandler(this, node.Path);

            node.Handlers = handlers;
        }

        _cacheHandler = new CacheHandler(this);
    }

    private async Task<bool> TryConnectAsync()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("TryConnect start");
        var address = await GetAccessibilityBusAddressAsync();
        if (string.IsNullOrWhiteSpace(address))
        {
            await Console.Error.WriteLineAsync("Failed to resolve the accessibility bus address.");
            return false;
        }
        _a11yAddress = address;

        try
        {
            LogVerbose("Opening private accessibility bus connection");
            _a11yConnection = await DBusConnection.ConnectAsync(address, _diagnostics);
            _uniqueName = await _a11yConnection.GetUniqueNameAsync() ?? "";
            LogVerbose($"TryConnect end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Failed to open accessibility bus: {ex.Message}");
            LogVerbose($"TryConnect failed after {sw.ElapsedMilliseconds} ms");
            _a11yConnection = null;
            return false;
        }
    }

    private async Task<string> GetAccessibilityBusAddressAsync()
    {
        var sw = Stopwatch.StartNew();
        LogVerbose("GetAddress start");
        try
        {
            LogVerbose("Connecting to session bus for org.a11y.Bus");
            await using var connection = await DBusConnection.ConnectSessionAsync(_diagnostics);
            var proxy = new OrgA11yBusProxy(connection, BusNameA11y, new DBusObjectPath(PathA11y));
            LogVerbose("Calling org.a11y.Bus.GetAddress");
            return await proxy.GetAddressAsync();
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"GetAddress call failed: {ex.Message}");
            return string.Empty;
        }
        finally
        {
            LogVerbose($"GetAddress end ({sw.ElapsedMilliseconds} ms)");
        }
    }

    private async Task<bool> RegisterObjectPathsAsync()
    {
        var sw = Stopwatch.StartNew();
        if (_a11yConnection == null)
        {
            Console.Error.WriteLine("Missing accessibility bus connection.");
            return false;
        }

        try
        {
            await RefreshPathRegistrationsAsync();
            LogVerbose($"RegisterObjectPaths end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to register object path fallback: {ex.Message}");
            return false;
        }
    }

    private async Task RefreshPathRegistrationsAsync()
    {
        if (_a11yConnection == null)
        {
            return;
        }

        var desiredRegistrations = new Dictionary<string, (object Owner, Func<Task<IDisposable>> Register)>(StringComparer.Ordinal);
        foreach (var node in _tree.NodesByPath.Values.OrderBy(static n => n.Path, StringComparer.Ordinal))
        {
            if (node.Handlers == null)
                continue;

            var handlers = node.Handlers;
            desiredRegistrations.Add(
                node.Path,
                (handlers, () => handlers.Register(_a11yConnection)));
        }

        if (_cacheHandler != null)
        {
            var cacheHandler = _cacheHandler;
            desiredRegistrations.Add(
                CachePath,
                (cacheHandler, () => _a11yConnection.RegisterObjects(CachePath, [cacheHandler])));
        }

        foreach (var (path, active) in _pathRegistrations.ToArray())
        {
            if (!desiredRegistrations.TryGetValue(path, out var desired)
                || !ReferenceEquals(active.Owner, desired.Owner))
            {
                active.Registration.Dispose();
                _pathRegistrations.Remove(path);
            }
        }

        foreach (var (path, desired) in desiredRegistrations)
        {
            if (_pathRegistrations.ContainsKey(path))
                continue;

            var registration = await desired.Register();
            _pathRegistrations.Add(path, new ActivePathRegistration(desired.Owner, registration));
        }
    }

    private async Task<bool> EmbedApplicationAsync()
    {
        if (string.IsNullOrWhiteSpace(_a11yAddress))
        {
            await Console.Error.WriteLineAsync("Missing accessibility bus address.");
            return false;
        }
        if (_a11yConnection == null)
        {
            await Console.Error.WriteLineAsync("Missing accessibility bus connection.");
            return false;
        }

        var sw = Stopwatch.StartNew();
        LogVerbose("EmbedApplication start");
        try
        {
            var proxy = new OrgA11yAtspiSocketProxy(_a11yConnection, BusNameRegistry, new DBusObjectPath(RootPath));
            LogVerbose("Calling org.a11y.atspi.Socket.Embed");
            var reply = await proxy.EmbedAsync(new AtSpiObjectReference(_uniqueName, new DBusObjectPath(RootPath)));
            var registryBus = reply.Service;
            var registryPath = reply.Path;
            Console.WriteLine($"Registry root: {registryBus} {registryPath}");
            LogVerbose($"EmbedApplication end ({sw.ElapsedMilliseconds} ms)");
            return true;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Embed call failed: {ex.Message}");
            LogVerbose($"EmbedApplication failed after {sw.ElapsedMilliseconds} ms");
            return false;
        }
    }

    private async Task InitializeRegistryEventTrackingAsync()
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
                    _registeredEvents.Add(registered.EventName);
                }
                UpdateEventMaskLocked();
            }

            var registryOwner = await ResolveRegistryUniqueNameAsync();
            await UpdateRegistrySignalSubscriptionsAsync(registryOwner);

            _registryOwnerChangedSubscription ??= await _a11yConnection.WatchNameOwnerChangedAsync(
                (name, _, newOwner) =>
                {
                    if (!string.Equals(name, BusNameRegistry, StringComparison.Ordinal))
                    {
                        return;
                    }

                    FireAndForget(UpdateRegistrySignalSubscriptionsAsync(newOwner));
                },
                emitOnCapturedContext: false);
        }
        catch (Exception ex)
        {
            LogVerbose($"Registry event tracking unavailable: {ex.Message}");
            _emitObjectEvents = true;
        }
    }

    private void OnRegistryEventListenerRegistered(string bus, string @event, List<string> properties)
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

    private async Task<string?> ResolveRegistryUniqueNameAsync()
    {
        if (_a11yConnection == null)
        {
            return null;
        }

        try
        {
            return await _a11yConnection.GetNameOwnerAsync(BusNameRegistry);
        }
        catch (Exception ex)
        {
            LogVerbose($"GetNameOwner failed: {ex.Message}");
        }

        return null;
    }

    private async Task UpdateRegistrySignalSubscriptionsAsync(string? registryOwner)
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

            var senderFilter = string.IsNullOrWhiteSpace(registryOwner) ? null : registryOwner;

            _registryProxy ??= new OrgA11yAtspiRegistryProxy(
                _a11yConnection,
                BusNameRegistry,
                new DBusObjectPath(RegistryPath));

            IDisposable? registered = null;
            IDisposable? deregistered = null;
            try
            {
                registered = await _registryProxy.WatchEventListenerRegisteredAsync(
                    OnRegistryEventListenerRegistered,
                    senderFilter,
                    emitOnCapturedContext: false);

                deregistered = await _registryProxy.WatchEventListenerDeregisteredAsync(
                    OnRegistryEventListenerDeregistered,
                    senderFilter,
                    emitOnCapturedContext: false);
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

    private static void FireAndForget(Task task)
    {
        _ = task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
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

    internal AtSpiAccessibleCacheItem BuildCacheItem(AccessibleNode node)
    {
        var self = new AtSpiObjectReference(_uniqueName, new DBusObjectPath(node.Path));
        var app = new AtSpiObjectReference(_uniqueName, new DBusObjectPath(RootPath));
        var parent = node.Parent == null
            ? new AtSpiObjectReference(string.Empty, new DBusObjectPath(NullPath))
            : new AtSpiObjectReference(_uniqueName, new DBusObjectPath(node.Parent.Path));
        var indexInParent = node.Parent == null ? -1 : node.Parent.Children.IndexOf(node);
        var childCount = node.Children.Count;
        var interfaces = node.Interfaces.Count == 0
            ? new List<string>()
            : new List<string>(node.Interfaces.OrderBy(static iface => iface, StringComparer.Ordinal).ToArray());
        var name = node.Name;
        var role = (uint)node.Role;
        var description = node.Description;
        var states = BuildStateSet(node.States);

        return new AtSpiAccessibleCacheItem(
            self,
            app,
            parent,
            indexInParent,
            childCount,
            interfaces,
            name,
            role,
            description,
            states);
    }

    private async Task CleanupAttemptAsync()
    {
        if (_toggleTimer != null)
        {
            await _toggleTimer.DisposeAsync();
            _toggleTimer = null;
        }

        foreach (var registration in _pathRegistrations.Values)
            registration.Registration.Dispose();
        _pathRegistrations.Clear();

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

    private static void LogSampleFlags()
    {
        var modifiers = AtSpiModifierMask.Shift | AtSpiModifierMask.Control;
        var eventTypes = AtSpiEventTypeMask.KeyPress | AtSpiEventTypeMask.KeyRelease;

        if (modifiers.HasFlag(AtSpiModifierMask.Shift))
        {
            LogVerbose($"Sample flags: modifiers={modifiers}, eventTypes={eventTypes}");
        }
    }

    private async Task CleanupAsync()
    {
        await CleanupAttemptAsync();

        Console.CancelKeyPress -= OnCancelKeyPress;
        _sigintRegistration?.Dispose();
        _sigintRegistration = null;
        _sigtermRegistration?.Dispose();
        _sigtermRegistration = null;
        if (_forceExitTimer is not null)
            await _forceExitTimer.DisposeAsync();
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
        _forceExitTimer ??= new Timer(
            _ => Environment.Exit(0),
            null,
            2000,
            Timeout.Infinite);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        RequestShutdown();
    }

    private void StartToggleLoop()
    {
        _toggleTimer = new Timer(
            _ => _ = ToggleWindowAsync(),
            null,
            WindowToggleIntervalMs,
            WindowToggleIntervalMs);
    }

    private async Task ToggleWindowAsync()
    {
        if (!_running || _a11yConnection == null)
        {
            return;
        }

        if (_tree.IsToggleWindowAttached)
        {
            var index = _tree.GetToggleWindowIndex();
            _tree.RemoveToggleWindow();
            await RefreshPathRegistrationsAsync();
            EmitChildrenChanged(_tree.Root, "remove", index < 0 ? 0 : index, _tree.ToggleWindow);
            EmitCacheRemoveSubtree(_tree.ToggleWindow);
        }
        else
        {
            _windowToggleCounter++;
            _tree.ToggleWindow.Description = $"Recurring window (cycle {_windowToggleCounter})";
            _tree.AddToggleWindow();
            await RefreshPathRegistrationsAsync();
            var index = _tree.GetToggleWindowIndex();
            EmitChildrenChanged(_tree.Root, "add", index < 0 ? 0 : index, _tree.ToggleWindow);
            EmitPropertyChange(_tree.ToggleWindow, "accessible-description", _tree.ToggleWindow.Description);
            EmitCacheAddSubtree(_tree.ToggleWindow);
        }
    }

    internal AtSpiObjectReference GetReference(AccessibleNode? node)
    {
        if (node == null)
        {
            return new AtSpiObjectReference(string.Empty, new DBusObjectPath(NullPath));
        }

        return new AtSpiObjectReference(_uniqueName, new DBusObjectPath(node.Path));
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

        var handlers = parent.Handlers;
        if (handlers?.EventObjectHandler == null)
        {
            return;
        }

        var reference = GetReference(child);
        var childVariant = new DBusVariant(reference.ToDbusStruct());
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

        var handlers = node.Handlers;
        if (handlers?.EventObjectHandler == null)
        {
            return;
        }

        handlers.EventObjectHandler.EmitPropertyChangeSignal(propertyName, new DBusVariant(value));
    }

    private sealed class ActivePathRegistration(object owner, IDisposable registration)
    {
        public object Owner { get; } = owner;

        public IDisposable Registration { get; } = registration;
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
        return _roleNames.GetValueOrDefault(role, "unknown");
    }
}
