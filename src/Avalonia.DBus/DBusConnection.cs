using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Avalonia.DBus;

public sealed class DBusConnection : IDBusConnection
{
    private readonly object _gate = new();
    private readonly Dictionary<ObjectHandlerKey, ObjectHandlerRegistration> _handlers = new();
    private readonly Dictionary<string, DBusRegisteredPathEntry> _registeredPathEntries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _activePathRefCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<ChildEdgeKey, int> _childEdgeRefCounts = new();
    private readonly Dictionary<string, HashSet<string>> _childNodesByPath = new(StringComparer.Ordinal);
    private readonly List<SignalSubscription> _subscriptions = [];
    private readonly CancellationTokenSource _dispatchCts = new();
    private readonly Task _dispatchLoop;
    private readonly DBusLogger _logger;
    private readonly DBusBuiltIns _builtIns = new();
    private long _nextRegistrationId = 1;
    private bool _disposed;

    private DBusConnection(DBusWireConnection wire, DBusLogger? loggers)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
        _logger = loggers ?? DBusLogger.CreateDefault();
        _dispatchLoop = Task.Run(() => DispatchLoopAsync(_dispatchCts.Token));
    }

    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    public static async Task<DBusConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectAsync(address, cancellationToken);
        return new DBusConnection(wire, loggers: null);
    }

    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    public static async Task<DBusConnection> ConnectAsync(
        string address,
        DBusLogger? loggers,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectAsync(address, loggers, cancellationToken);
        return new DBusConnection(wire, loggers);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(cancellationToken);
        return new DBusConnection(wire, loggers: null);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        DBusLogger? loggers,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(loggers, cancellationToken);
        return new DBusConnection(wire, loggers);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(cancellationToken);
        return new DBusConnection(wire, loggers: null);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        DBusLogger? loggers,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(loggers, cancellationToken);
        return new DBusConnection(wire, loggers);
    }

    /// <summary>
    /// The underlying DBus wire connection.
    /// </summary>
    private DBusWireConnection Wire { get; }

    /// <summary>
    /// Sends a pre-constructed message without waiting for a reply.
    /// </summary>
    public object CreateProxy(
        Type interfaceType,
        string destination,
        DBusObjectPath path,
        string? iface = null)
    {
        return DBusWrapperMetadata.CreateProxy(interfaceType, this, destination, path, iface);
    }

    public Task SendMessageAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Wire.SendAsync(message, cancellationToken);
    }

    /// <summary>
    /// Calls a method on a remote object and returns the reply.
    /// </summary>
    public async Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        CancellationToken cancellationToken = default,
        params object[] args)
    {
        LogVerbose($"CallMethod start: dest='{destination}' path='{path}' iface='{iface}' member='{member}' args={(args?.Length ?? 0)}");
        var message = DBusMessage.CreateMethodCall(destination, path, iface, member, args ?? []);
        var reply = await Wire.SendWithReplyAsync(message, cancellationToken);
        LogVerbose($"CallMethod reply: type={reply.Type} replySerial={reply.ReplySerial} error='{reply.ErrorName}' body={reply.Body.Count}");
        ThrowIfError(reply);
        return reply;
    }

    /// <summary>
    /// Subscribes to signals matching the specified criteria.
    /// </summary>
    public async Task<IDisposable> SubscribeAsync(
        string? sender,
        DBusObjectPath? path,
        string iface,
        string member,
        Func<DBusMessage, Task> handler,
        SynchronizationContext? synchronizationContext = null)
    {
        if (string.IsNullOrEmpty(iface))
            throw new ArgumentException("Interface is required.", nameof(iface));
        if (string.IsNullOrEmpty(member))
            throw new ArgumentException("Member is required.", nameof(member));

        ArgumentNullException.ThrowIfNull(handler);

        var matchRule = BuildMatchRule(sender, path, iface, member);
        await AddMatchAsync(matchRule);

        var subscription = new SignalSubscription(this, sender, path, iface, member, handler, synchronizationContext, matchRule);
        lock (_gate)
        {
            ThrowIfDisposed();
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Subscribes to the org.freedesktop.DBus NameOwnerChanged signal.
    /// </summary>
    public Task<IDisposable> WatchNameOwnerChangedAsync(
        Action<string, string?, string?> handler,
        bool emitOnCapturedContext = true,
        string? sender = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeAsync(
            sender,
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameOwnerChanged",
            message =>
            {
                var name = (string)message.Body[0];
                var oldOwner = NormalizeNameOwner((string)message.Body[1]);
                var newOwner = NormalizeNameOwner((string)message.Body[2]);
                handler(name, oldOwner, newOwner);
                return Task.CompletedTask;
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    /// <summary>
    /// Subscribes to the org.freedesktop.DBus NameOwnerChanged signal.
    /// </summary>
    public Task<IDisposable> WatchNameOwnerChangedAsync(
        Func<string, string?, string?, Task> handler,
        bool emitOnCapturedContext = true,
        string? sender = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        return SubscribeAsync(
            sender,
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameOwnerChanged",
            message =>
            {
                var name = (string)message.Body[0];
                var oldOwner = NormalizeNameOwner((string)message.Body[1]);
                var newOwner = NormalizeNameOwner((string)message.Body[2]);
                return handler(name, oldOwner, newOwner);
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    /// <summary>
    /// Requests ownership of a bus name.
    /// </summary>
    public async Task<DBusRequestNameReply> RequestNameAsync(
        string name,
        DBusRequestNameFlags flags = DBusRequestNameFlags.None,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required.", nameof(name));

        var reply = await CallBusMethodAsync(
            "RequestName",
            cancellationToken,
            name,
            (uint)flags);

        if (reply.Body.Count == 0)
            throw new InvalidOperationException("RequestName returned no reply.");

        var value = reply.Body[0] switch
        {
            uint u => u,
            int i => unchecked((uint)i),
            _ => throw new InvalidOperationException("RequestName returned an unexpected value.")
        };

        return (DBusRequestNameReply)value;
    }

    /// <summary>
    /// Releases ownership of a bus name.
    /// </summary>
    public async Task ReleaseNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required.", nameof(name));

        await CallBusMethodAsync("ReleaseName", cancellationToken, name);
    }

    /// <summary>
    /// Resolves the unique name that currently owns a well-known bus name.
    /// </summary>
    public async Task<string?> GetNameOwnerAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name is required.", nameof(name));

        DBusMessage reply;
        try
        {
            reply = await CallBusMethodAsync("GetNameOwner", cancellationToken, name);
        }
        catch (DBusException ex) when (string.Equals(ex.ErrorName, "org.freedesktop.DBus.Error.NameHasNoOwner", StringComparison.Ordinal))
        {
            return null;
        }

        if (reply.Body.Count == 0)
            throw new InvalidOperationException("GetNameOwner returned no reply.");

        if (reply.Body[0] is string owner)
            return string.IsNullOrWhiteSpace(owner) ? null : owner;

        throw new InvalidOperationException("GetNameOwner returned an unexpected value.");
    }

    /// <summary>
    /// Registers a handler for method calls on the specified path and interface.
    /// </summary>
    public IDisposable RegisterObject(
        DBusObjectPath path,
        string iface,
        Func<DBusConnection, DBusMessage, Task<DBusMessage>> handler,
        SynchronizationContext? synchronizationContext = null)
    {
        if (string.IsNullOrEmpty(iface))
            throw new ArgumentException("Interface is required.", nameof(iface));

        ArgumentNullException.ThrowIfNull(handler);

        var normalizedPath = NormalizePath(path.Value);
        var key = new ObjectHandlerKey(normalizedPath, iface);
        var registration = new ObjectHandlerRegistration(this, key, handler, synchronizationContext);

        lock (_gate)
        {
            ThrowIfDisposed();

            if (_registeredPathEntries.ContainsKey(normalizedPath))
            {
                throw new InvalidOperationException(
                    $"Path '{normalizedPath}' is already occupied by explicit exported-target registration.");
            }

            if (_handlers.ContainsKey(key))
            {
                throw new InvalidOperationException("A handler is already registered for this path and interface.");
            }

            _handlers.Add(key, registration);
        }

        return registration;
    }

    /// <summary>
    /// Registers a service target for dispatch at the specified full object path.
    /// </summary>
    public IDisposable Register(
        string fullPath,
        DBusExportedTarget target,
        SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(target);

        var normalizedPath = NormalizePath(fullPath);
        long registrationId;
        lock (_gate)
        {
            ThrowIfDisposed();

            if (_registeredPathEntries.ContainsKey(normalizedPath))
                throw new InvalidOperationException($"An exported target is already registered for path '{normalizedPath}'.");

            EnsureNoLegacyPathCollisionLocked(normalizedPath, target);
            var entry = CreateRegisteredPathEntryLocked(normalizedPath, target, context);
            _registeredPathEntries.Add(normalizedPath, entry);
            IncrementPathRefCountsLocked(normalizedPath);
            registrationId = entry.RegistrationId;
        }

        return new ExportedPathRegistration(this, normalizedPath, registrationId);
    }

    /// <summary>
    /// Applies a batch of add/remove/replace registration operations atomically.
    /// </summary>
    public void ApplyRegistrationBatch(
        IEnumerable<DBusRegistrationOperation> operations,
        SynchronizationContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var operationArray = operations as DBusRegistrationOperation[] ?? operations.ToArray();
        if (operationArray.Length == 0)
            return;

        lock (_gate)
        {
            ThrowIfDisposed();

            var candidate = new Dictionary<string, DBusRegisteredPathEntry>(_registeredPathEntries, StringComparer.Ordinal);
            foreach (var operation in operationArray)
            {
                var path = NormalizePath(operation.Path);
                switch (operation.Kind)
                {
                    case RegistrationOperationKind.Add:
                    {
                        if (candidate.ContainsKey(path))
                            throw new InvalidOperationException($"An exported target is already registered for path '{path}'.");

                        var exportedTarget = CoerceExportedTarget(operation.Target, path);
                        EnsureNoLegacyPathCollisionLocked(path, exportedTarget);
                        candidate[path] = CreateRegisteredPathEntryLocked(path, exportedTarget, context);
                        break;
                    }
                    case RegistrationOperationKind.Remove:
                    {
                        if (!candidate.Remove(path))
                            throw new InvalidOperationException($"No exported target is registered for path '{path}'.");
                        break;
                    }
                    case RegistrationOperationKind.Replace:
                    {
                        if (!candidate.ContainsKey(path))
                            throw new InvalidOperationException($"No exported target is registered for path '{path}'.");

                        var exportedTarget = CoerceExportedTarget(operation.Target, path);
                        EnsureNoLegacyPathCollisionLocked(path, exportedTarget);
                        candidate[path] = CreateRegisteredPathEntryLocked(path, exportedTarget, context);
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unsupported registration operation '{operation.Kind}'.");
                }
            }

            CommitRegisteredPathEntriesLocked(candidate);
        }
    }

    /// <summary>
    /// Returns direct child object paths for a parent path.
    /// </summary>
    public IReadOnlyList<string> QueryChildren(string path)
    {
        var normalizedPath = NormalizePath(path);
        lock (_gate)
        {
            ThrowIfDisposed();

            return QueryChildPathsLocked(normalizedPath)
                .OrderBy(static x => x, StringComparer.Ordinal)
                .ToArray();
        }
    }

    /// <summary>
    /// Returns true when an explicit exported target is currently registered at the exact path.
    /// </summary>
    public bool IsPathRegistered(string path)
    {
        var normalizedPath = NormalizePath(path);
        lock (_gate)
        {
            ThrowIfDisposed();
            return _registeredPathEntries.ContainsKey(normalizedPath);
        }
    }

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _registeredPathEntries.Clear();
            _activePathRefCounts.Clear();
            _childEdgeRefCounts.Clear();
            _childNodesByPath.Clear();
            _handlers.Clear();
            _subscriptions.Clear();
        }

        await _dispatchCts.CancelAsync();

        try
        {
            await _dispatchLoop;
        }
        catch
        {
            // Ignore dispatch loop failures on shutdown.
        }

        await Wire.DisposeAsync();
        _dispatchCts.Dispose();
    }

    private async Task DispatchLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in Wire.ReceiveAsync(cancellationToken))
        {
            if (message.Type == DBusMessageType.Signal)
            {
                DispatchSignal(message);
            }
            else if (message.Type == DBusMessageType.MethodCall)
            {
                DispatchMethodCall(message);
            }
        }
    }

    private void DispatchSignal(DBusMessage message)
    {
        List<SignalSubscription> snapshot;
        lock (_gate)
        {
            snapshot = _subscriptions.ToList();
        }

        foreach (var subscription in snapshot)
        {
            if (subscription.IsMatch(message))
            {
                subscription.Invoke(message);
            }
        }
    }

    private void DispatchMethodCall(DBusMessage message)
    {
        if (!message.Path.HasValue || string.IsNullOrEmpty(message.Interface))
            return;

        var pathValue = (string)message.Path.Value;

        ObjectHandlerRegistration? registration;
        DBusRegisteredPathEntry? registeredPathEntry;
        bool hasLegacyPath;
        bool hasActivePath;
        var key = new ObjectHandlerKey(pathValue, message.Interface);
        lock (_gate)
        {
            _handlers.TryGetValue(key, out registration);
            _registeredPathEntries.TryGetValue(pathValue, out registeredPathEntry);
            hasLegacyPath = _handlers.Keys.Any(k => string.Equals(k.Path, pathValue, StringComparison.Ordinal));
            hasActivePath = _activePathRefCounts.ContainsKey(pathValue);
        }

        if (registration != null)
        {
            LogVerbose($"Dispatch METHOD_CALL: path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
            registration.Invoke(message);
            return;
        }

        if (registeredPathEntry != null)
        {
            DispatchExportedPathCall(message, pathValue, registeredPathEntry);
            return;
        }

        if (hasActivePath)
        {
            DispatchVirtualPathCall(message, pathValue);
            return;
        }

        ReplyMissingHandler(message, hasLegacyPath);
    }

    private void DispatchVirtualPathCall(DBusMessage message, string path)
    {
        LogVerbose($"Dispatch METHOD_CALL (virtual): path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
        FireAndForget(HandleVirtualPathCallAsync(message, path));
    }

    private async Task HandleVirtualPathCallAsync(DBusMessage message, string path)
    {
        DBusMessage reply;
        try
        {
            var introspectionXml = BuildIntrospectionXml(path, exportedEntry: null);

            reply = _builtIns.TryHandlePeer(message)
                    ?? _builtIns.TryHandleProperties(message, entry: null)
                    ?? _builtIns.TryHandleIntrospectable(message, introspectionXml)
                    ?? message.CreateError(
                        "org.freedesktop.DBus.Error.UnknownInterface",
                        $"No handler registered for interface '{message.Interface}' on '{path}'.");
        }
        catch (Exception ex)
        {
            reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
        }

        reply = EnsureReplyMetadata(message, reply);
        await Wire.SendAsync(reply);
    }

    private void DispatchExportedPathCall(DBusMessage message, string path, DBusRegisteredPathEntry registeredPathEntry)
    {
        LogVerbose($"Dispatch METHOD_CALL (exported): path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
        var synchronizationContext = ResolveExportedPathSynchronizationContext(message, registeredPathEntry);
        if (synchronizationContext == null)
        {
            FireAndForget(HandleExportedPathCallAsync(message, path, registeredPathEntry));
        }
        else
        {
            synchronizationContext.Post(
                _ => FireAndForget(HandleExportedPathCallAsync(message, path, registeredPathEntry)),
                null);
        }
    }

    private async Task HandleExportedPathCallAsync(
        DBusMessage message,
        string path,
        DBusRegisteredPathEntry registeredPathEntry)
    {
        DBusMessage reply;
        try
        {
            var introspectionXml = BuildIntrospectionXml(path, registeredPathEntry);

            reply = _builtIns.TryHandlePeer(message)
                    ?? _builtIns.TryHandleProperties(message, registeredPathEntry)
                    ?? _builtIns.TryHandleIntrospectable(message, introspectionXml)
                    ?? await InvokeExportedTargetMemberAsync(message, registeredPathEntry);
        }
        catch (Exception ex)
        {
            reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
        }

        reply = EnsureReplyMetadata(message, reply);
        await Wire.SendAsync(reply);
    }

    private async Task<DBusMessage> InvokeExportedTargetMemberAsync(DBusMessage message, DBusRegisteredPathEntry entry)
    {
        var iface = message.Interface;
        if (string.IsNullOrWhiteSpace(iface))
        {
            return message.CreateError(
                "org.freedesktop.DBus.Error.UnknownInterface",
                $"No handler registered for interface '<null>' on '{entry.Path}'.");
        }

        if (!entry.TryGetBinding(iface, out var binding))
        {
            return message.CreateError(
                "org.freedesktop.DBus.Error.UnknownInterface",
                $"No handler registered for interface '{iface}' on '{entry.Path}'.");
        }

        var reply = await binding.Descriptor.Dispatcher.Handle(message, this, binding.Target);
        return reply ?? message.CreateError("org.freedesktop.DBus.Error.Failed", "Dispatcher returned null reply.");
    }

    private static SynchronizationContext? ResolveExportedPathSynchronizationContext(
        DBusMessage message,
        DBusRegisteredPathEntry entry)
    {
        if (!string.IsNullOrEmpty(message.Interface) && entry.TryGetBinding(message.Interface, out var binding))
            return binding.SynchronizationContext ?? entry.DefaultSynchronizationContext;

        return entry.DefaultSynchronizationContext;
    }

    private void ReplyMissingHandler(DBusMessage message, bool hasPath)
    {
        if ((message.Flags & DBusMessageFlags.NoReplyExpected) != 0)
            return;

        var iface = string.IsNullOrWhiteSpace(message.Interface) ? "<null>" : message.Interface;
        var member = string.IsNullOrWhiteSpace(message.Member) ? "<null>" : message.Member;
        var path = message.Path.HasValue ? (string)message.Path.Value : "<null>";

        string errorName;
        string errorMessage;
        if (hasPath)
        {
            errorName = "org.freedesktop.DBus.Error.UnknownInterface";
            errorMessage = $"No handler registered for interface '{iface}' on '{path}'.";
        }
        else
        {
            errorName = "org.freedesktop.DBus.Error.UnknownObject";
            errorMessage = $"No handler registered for object '{path}'.";
        }

        LogVerbose($"Dispatch METHOD_CALL missing handler: path='{path}' iface='{iface}' member='{member}' -> {errorName}");
        var error = message.CreateError(errorName, errorMessage);
        FireAndForget(Wire.SendAsync(error));
    }

    private IEnumerable<string> QueryChildPathsLocked(string parentPath)
    {
        if (!_activePathRefCounts.ContainsKey(parentPath))
            return [];

        if (!_childNodesByPath.TryGetValue(parentPath, out var children) || children.Count == 0)
            return [];

        return children.Select(childName => parentPath == "/" ? "/" + childName : parentPath + "/" + childName);
    }

    private static DBusExportedTarget CoerceExportedTarget(DBusExportedTarget? target, string path)
    {
        return target ?? throw new InvalidOperationException(
            $"Registration for path '{path}' requires an explicit exported target. " +
            "Use DBusExportedTarget.Create(...) and generated binding helpers.");
    }

    private void EnsureNoLegacyPathCollisionLocked(string path, DBusExportedTarget exportedTarget)
    {
        if (_handlers.Keys.Any(key => string.Equals(key.Path, path, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Path '{path}' is already occupied by legacy path/interface handlers. " +
                "Legacy and exported-target registrations cannot share the same exact path.");
        }

        if (exportedTarget.BoundInterfaces.Count == 0)
        {
            throw new InvalidOperationException(
                $"Registration for path '{path}' must provide at least one bound interface.");
        }
    }

    private DBusRegisteredPathEntry CreateRegisteredPathEntryLocked(
        string path,
        DBusExportedTarget exportedTarget,
        SynchronizationContext? defaultSynchronizationContext)
    {
        var interfacesByName = new Dictionary<string, DBusBoundInterfaceRegistration>(StringComparer.Ordinal);
        for (var i = 0; i < exportedTarget.BoundInterfaces.Count; i++)
        {
            var boundInterface = exportedTarget.BoundInterfaces[i]
                                 ?? throw new InvalidOperationException(
                                     $"Registration for path '{path}' contains a null interface binding at index {i}.");

            var descriptor = boundInterface.Descriptor
                             ?? throw new InvalidOperationException(
                                 $"Registration for path '{path}' contains a binding with no descriptor at index {i}.");

            if (!DBusGeneratedMetadata.TryGetByInterfaceName(descriptor.InterfaceName, out var canonicalByName)
                || !DBusGeneratedMetadata.TryGetByClrType(descriptor.ClrInterfaceType, out var canonicalByClrType))
            {
                throw new InvalidOperationException(
                    $"Descriptor '{descriptor.InterfaceName}' / '{descriptor.ClrInterfaceType.FullName}' is not registered. " +
                    "Ensure generated module initializers have run before registering the target.");
            }

            if (!ReferenceEquals(canonicalByName, canonicalByClrType))
            {
                throw new InvalidOperationException(
                    $"Descriptor registry is inconsistent for interface '{descriptor.InterfaceName}' and CLR type '{descriptor.ClrInterfaceType.FullName}'.");
            }

            if (interfacesByName.ContainsKey(canonicalByName.InterfaceName))
            {
                throw new InvalidOperationException(
                    $"Registration for path '{path}' contains duplicate interface '{canonicalByName.InterfaceName}'.");
            }

            interfacesByName.Add(
                canonicalByName.InterfaceName,
                new DBusBoundInterfaceRegistration(
                    canonicalByName,
                    boundInterface.Target,
                    boundInterface.SynchronizationContext ?? defaultSynchronizationContext));
        }

        if (interfacesByName.Count == 0)
        {
            throw new InvalidOperationException(
                $"Registration for path '{path}' must provide at least one bound interface.");
        }

        var registrationId = _nextRegistrationId++;
        return new DBusRegisteredPathEntry(
            registrationId,
            path,
            exportedTarget,
            interfacesByName,
            defaultSynchronizationContext);
    }

    private void CommitRegisteredPathEntriesLocked(Dictionary<string, DBusRegisteredPathEntry> candidate)
    {
        var removedPaths = _registeredPathEntries.Keys
            .Where(path => !candidate.ContainsKey(path))
            .ToArray();

        var addedPaths = candidate.Keys
            .Where(path => !_registeredPathEntries.ContainsKey(path))
            .ToArray();

        _registeredPathEntries.Clear();
        foreach (var (path, entry) in candidate)
        {
            _registeredPathEntries[path] = entry;
        }

        foreach (var removedPath in removedPaths)
        {
            DecrementPathRefCountsLocked(removedPath);
        }

        foreach (var addedPath in addedPaths)
        {
            IncrementPathRefCountsLocked(addedPath);
        }
    }

    private void TryUnregisterExportedPath(string path, long registrationId)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (!_registeredPathEntries.TryGetValue(path, out var current))
                return;

            if (current.RegistrationId != registrationId)
                return;

            _registeredPathEntries.Remove(path);
            DecrementPathRefCountsLocked(path);
        }
    }

    private string BuildIntrospectionXml(
        string path,
        DBusRegisteredPathEntry? exportedEntry)
    {
        string[] children;
        lock (_gate)
        {
            children = _childNodesByPath.TryGetValue(path, out var names)
                ? names.OrderBy(static x => x, StringComparer.Ordinal).ToArray()
                : [];
        }

        var doc = new XmlDocument();
        doc.LoadXml("<node/>");
        var root = doc.DocumentElement ?? throw new InvalidOperationException("Failed to create introspection root.");

        if (exportedEntry != null)
        {
            foreach (var (_, binding) in exportedEntry.InterfacesByName.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                AppendXmlFragment(doc, root, binding.Descriptor.IntrospectionXml);
            }
        }

        if (_builtIns.EnablePropertiesInterface)
            AppendXmlFragment(doc, root, DBusBuiltIns.PropertiesIntrospectXml);

        if (_builtIns.EnableIntrospectableInterface)
            AppendXmlFragment(doc, root, DBusBuiltIns.IntrospectableIntrospectXml);

        if (_builtIns.EnablePeerInterface)
            AppendXmlFragment(doc, root, DBusBuiltIns.PeerIntrospectXml);

        foreach (var child in children)
        {
            var childNode = doc.CreateElement("node");
            var childName = doc.CreateAttribute("name");
            childName.Value = child;
            childNode.Attributes.Append(childName);
            root.AppendChild(childNode);
        }

        return root.OuterXml;
    }

    private static void AppendXmlFragment(XmlDocument targetDocument, XmlNode targetNode, string xmlFragment)
    {
        if (string.IsNullOrWhiteSpace(xmlFragment))
            return;

        var fragmentDocument = new XmlDocument();
        fragmentDocument.LoadXml("<root>" + xmlFragment + "</root>");
        var fragmentRoot = fragmentDocument.DocumentElement;
        if (fragmentRoot == null)
            return;

        foreach (XmlNode child in fragmentRoot.ChildNodes)
        {
            var imported = targetDocument.ImportNode(child, deep: true);
            targetNode.AppendChild(imported);
        }
    }

    private void IncrementPathRefCountsLocked(string path)
    {
        var segments = SplitPath(path);
        var current = "/";
        IncrementPathNodeRefLocked(current);

        foreach (var segment in segments)
        {
            var next = current == "/" ? "/" + segment : current + "/" + segment;
            IncrementPathNodeRefLocked(next);
            IncrementChildRefLocked(current, segment);
            current = next;
        }
    }

    private void DecrementPathRefCountsLocked(string path)
    {
        var segments = SplitPath(path);
        var current = "/";
        DecrementPathNodeRefLocked(current);

        foreach (var segment in segments)
        {
            var next = current == "/" ? "/" + segment : current + "/" + segment;
            DecrementPathNodeRefLocked(next);
            DecrementChildRefLocked(current, segment);
            current = next;
        }
    }

    private void IncrementPathNodeRefLocked(string path)
    {
        if (_activePathRefCounts.TryGetValue(path, out var count))
            _activePathRefCounts[path] = count + 1;
        else
            _activePathRefCounts[path] = 1;
    }

    private void DecrementPathNodeRefLocked(string path)
    {
        if (!_activePathRefCounts.TryGetValue(path, out var count))
            return;

        if (count <= 1)
            _activePathRefCounts.Remove(path);
        else
            _activePathRefCounts[path] = count - 1;
    }

    private void IncrementChildRefLocked(string parentPath, string childName)
    {
        var key = new ChildEdgeKey(parentPath, childName);
        if (_childEdgeRefCounts.TryGetValue(key, out var count))
        {
            _childEdgeRefCounts[key] = count + 1;
            return;
        }

        _childEdgeRefCounts[key] = 1;

        if (!_childNodesByPath.TryGetValue(parentPath, out var children))
        {
            children = new HashSet<string>(StringComparer.Ordinal);
            _childNodesByPath[parentPath] = children;
        }

        children.Add(childName);
    }

    private void DecrementChildRefLocked(string parentPath, string childName)
    {
        var key = new ChildEdgeKey(parentPath, childName);
        if (!_childEdgeRefCounts.TryGetValue(key, out var count))
            return;

        if (count > 1)
        {
            _childEdgeRefCounts[key] = count - 1;
            return;
        }

        _childEdgeRefCounts.Remove(key);

        if (!_childNodesByPath.TryGetValue(parentPath, out var children))
            return;

        children.Remove(childName);
        if (children.Count == 0)
            _childNodesByPath.Remove(parentPath);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        if (!path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));

        if (path.Length > 1 && path.EndsWith('/'))
            path = path.TrimEnd('/');

        return path;
    }

    private static string[] SplitPath(string path)
    {
        return path == "/" ? Array.Empty<string>() : path.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DBusConnection));
    }

    private void LogVerbose(string message)
    {
        var sink = _logger.Verbose;
        if (sink == null)
            return;

#if DEBUG
        sink($"[DBusConnection {Environment.CurrentManagedThreadId}] {message}");
#else
        sink(message);
#endif
    }

    private async Task<DBusMessage> CallBusMethodAsync(
        string member,
        CancellationToken cancellationToken,
        params object[] body)
    {
        var message = DBusMessage.CreateMethodCall(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            member,
            body);

        var reply = await Wire.SendWithReplyAsync(message, cancellationToken);
        ThrowIfError(reply);
        return reply;
    }

    private async Task AddMatchAsync(string rule)
    {
        await CallBusMethodAsync("AddMatch", CancellationToken.None, rule);
    }

    private void RemoveMatch(string rule)
    {
        FireAndForget(CallBusMethodAsync("RemoveMatch", CancellationToken.None, rule));
    }

    private void ThrowIfError(DBusMessage reply)
    {
        if (reply.Type != DBusMessageType.Error)
            return;

        var errorName = reply.ErrorName ?? "org.freedesktop.DBus.Error.Failed";
        string? errorMessage = null;

        if (reply.Body.Count > 0 && reply.Body[0] is string message)
            errorMessage = message;

        LogVerbose($"D-Bus error reply: name='{errorName}' message='{errorMessage}'");
        throw new DBusException(errorName, errorMessage, reply);
    }

    private static DBusMessage EnsureReplyMetadata(DBusMessage request, DBusMessage reply)
    {
        if (reply.Type != DBusMessageType.MethodReturn && reply.Type != DBusMessageType.Error)
            return reply;

        var replySerial = reply.ReplySerial != 0 ? reply.ReplySerial : request.Serial;
        var destination = reply.Destination ?? request.Sender;
        var errorName = reply.ErrorName;

        if (reply.Type == DBusMessageType.Error && string.IsNullOrEmpty(errorName))
            errorName = "org.freedesktop.DBus.Error.Failed";

        if (reply.ReplySerial == replySerial
            && string.Equals(reply.Destination, destination, StringComparison.Ordinal)
            && string.Equals(reply.ErrorName, errorName, StringComparison.Ordinal))
        {
            return reply;
        }

        return new DBusMessage
        {
            Type = reply.Type,
            Flags = reply.Flags,
            ReplySerial = replySerial,
            Path = reply.Path,
            Interface = reply.Interface,
            Member = reply.Member,
            ErrorName = errorName,
            Destination = destination,
            Body = reply.Body
        };
    }

    private static string BuildMatchRule(string? sender, DBusObjectPath? path, string iface, string member)
    {
        List<string> parts = ["type='signal'"];

        if (!string.IsNullOrEmpty(sender))
            parts.Add($"sender='{EscapeMatchValue(sender)}'");

        if (path.HasValue)
            parts.Add($"path='{EscapeMatchValue(path.Value)}'");

        if (!string.IsNullOrEmpty(iface))
            parts.Add($"interface='{EscapeMatchValue(iface)}'");

        if (!string.IsNullOrEmpty(member))
            parts.Add($"member='{EscapeMatchValue(member)}'");

        return string.Join(",", parts);
    }

    private static string EscapeMatchValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", @"\\", StringComparison.Ordinal)
            .Replace("'", "\\'", StringComparison.Ordinal);
    }

    private static string? NormalizeNameOwner(string owner)
    {
        return string.IsNullOrWhiteSpace(owner) ? null : owner;
    }

    private static void FireAndForget(Task task)
    {
        _ = task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private readonly struct ChildEdgeKey(string parentPath, string childName) : IEquatable<ChildEdgeKey>
    {
        private readonly string _parentPath = parentPath ?? string.Empty;
        private readonly string _childName = childName ?? string.Empty;

        public bool Equals(ChildEdgeKey other)
            => string.Equals(_parentPath, other._parentPath, StringComparison.Ordinal)
               && string.Equals(_childName, other._childName, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ChildEdgeKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(StringComparer.Ordinal.GetHashCode(_parentPath), StringComparer.Ordinal.GetHashCode(_childName));
    }

    private sealed class ExportedPathRegistration(
        DBusConnection connection,
        string path,
        long registrationId)
        : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            connection.TryUnregisterExportedPath(path, registrationId);
        }
    }

    private readonly struct ObjectHandlerKey(string path, string iface) : IEquatable<ObjectHandlerKey>
    {
        private readonly string _path = path ?? string.Empty;
        private readonly string _iface = iface ?? string.Empty;

        public bool Equals(ObjectHandlerKey other)
            => string.Equals(_path, other._path, StringComparison.Ordinal)
               && string.Equals(_iface, other._iface, StringComparison.Ordinal);

        public string Path => _path;

        public override bool Equals(object? obj) => obj is ObjectHandlerKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(StringComparer.Ordinal.GetHashCode(_path), StringComparer.Ordinal.GetHashCode(_iface));
    }

    private sealed class SignalSubscription(
        DBusConnection connection,
        string? sender,
        DBusObjectPath? path,
        string iface,
        string member,
        Func<DBusMessage, Task> handler,
        SynchronizationContext? context,
        string matchRule)
        : IDisposable
    {
        private bool _disposed;

        public bool IsMatch(DBusMessage message)
        {
            if (message.Type != DBusMessageType.Signal)
                return false;

            if (!string.IsNullOrEmpty(sender) && !string.Equals(message.Sender, sender, StringComparison.Ordinal))
                return false;

            if (path.HasValue)
            {
                if (!message.Path.HasValue)
                    return false;

                if (message.Path.Value != path.Value)
                    return false;
            }

            if (!string.Equals(message.Interface, iface, StringComparison.Ordinal))
                return false;

            if (!string.Equals(message.Member, member, StringComparison.Ordinal))
                return false;

            return true;
        }

        public void Invoke(DBusMessage message)
        {
            if (_disposed)
                return;

            if (context == null)
                FireAndForget(handler(message));
            else
                context.Post(_ => FireAndForget(handler(message)), null);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            lock (connection._gate)
            {
                connection._subscriptions.Remove(this);
            }

            connection.RemoveMatch(matchRule);
        }
    }

    private sealed class ObjectHandlerRegistration(
        DBusConnection connection,
        ObjectHandlerKey key,
        Func<DBusConnection, DBusMessage, Task<DBusMessage>> handler,
        SynchronizationContext? context)
        : IDisposable
    {
        private bool _disposed;

        public void Invoke(DBusMessage message)
        {
            if (_disposed)
                return;

            if (context == null)
            {
                FireAndForget(HandleAsync(message));
            }
            else
            {
                context.Post(_ => FireAndForget(HandleAsync(message)), null);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            lock (connection._gate)
            {
                connection._handlers.Remove(key);
            }
        }

        private async Task HandleAsync(DBusMessage message)
        {
            DBusMessage reply;
            try
            {
                reply = await handler(connection, message);
                if (reply == null)
                    reply = message.CreateError("org.freedesktop.DBus.Error.Failed", "Handler returned null reply.");
            }
            catch (Exception ex)
            {
                reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
            }

            reply = EnsureReplyMetadata(message, reply);
            await connection.Wire.SendAsync(reply);
        }
    }

    public async Task<string?> GetUniqueNameAsync()
    {
        return await Wire.GetUniqueNameAsync();
    }
}
