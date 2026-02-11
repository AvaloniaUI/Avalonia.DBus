using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Avalonia.DBus;

public sealed class DBusConnection : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<ObjectHandlerKey, ObjectHandlerRegistration> _handlers = new();
    private readonly Dictionary<string, RegisteredObject> _registeredObjects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _activePathRefCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<ChildEdgeKey, int> _childEdgeRefCounts = new();
    private readonly Dictionary<string, HashSet<string>> _childNodesByPath = new(StringComparer.Ordinal);
    private readonly List<SignalSubscription> _subscriptions = [];
    private readonly CancellationTokenSource _dispatchCts = new();
    private readonly Task _dispatchLoop;
    private readonly DBusLogger _logger;
    private readonly DBusBuiltIns _builtIns = new();
    private readonly ObjectTreeRegistration _serviceTreeRegistration;
    private bool _disposed;

    private DBusConnection(DBusWireConnection wire, DBusLogger? loggers)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
        _logger = loggers ?? DBusLogger.CreateDefault();
        Root = new DBusObject("/");
        _serviceTreeRegistration = new ObjectTreeRegistration(this, synchronizationContext: null);

        lock (_gate)
        {
            RegisterNodeLocked(_serviceTreeRegistration, Root);
        }

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
    /// Root of the service-side object tree managed by this connection.
    /// </summary>
    public DBusObject Root { get; }

    /// <summary>
    /// Sends a pre-constructed message without waiting for a reply.
    /// </summary>
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
    public Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        params object[] args)
        => CallMethodAsync(destination, path, iface, member, CancellationToken.None, args);

    /// <summary>
    /// Calls a method on a remote object and returns the reply.
    /// </summary>
    public async Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        CancellationToken cancellationToken,
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
    /// <param name="sender">Filter by sender (null for any).</param>
    /// <param name="path">Filter by object path (null for any).</param>
    /// <param name="iface">Interface name.</param>
    /// <param name="member">Signal name.</param>
    /// <param name="handler">Async callback invoked for each matching signal.</param>
    /// <param name="synchronizationContext">
    /// Optional synchronization context to invoke the handler on (e.g., UI thread).
    /// If null, handler is invoked on the connection's internal thread.
    /// </param>
    /// <returns>Disposable that unsubscribes when disposed.</returns>
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
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Subscribes to the org.freedesktop.DBus NameOwnerChanged signal.
    /// </summary>
    /// <param name="handler">Invoked with (name, oldOwner, newOwner).</param>
    /// <param name="emitOnCapturedContext">Whether to invoke the handler on the captured context.</param>
    /// <param name="sender">Filter by sender (null for any).</param>
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
    /// <param name="handler">Invoked with (name, oldOwner, newOwner).</param>
    /// <param name="emitOnCapturedContext">Whether to invoke the handler on the captured context.</param>
    /// <param name="sender">Filter by sender (null for any).</param>
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
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        var reply = await CallBusMethodAsync(
                "RequestName",
                cancellationToken,
                name,
                (uint)flags);

        if (reply.Body.Count == 0)
        {
            throw new InvalidOperationException("RequestName returned no reply.");
        }

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
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

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

        var key = new ObjectHandlerKey(path.Value, iface);
        var registration = new ObjectHandlerRegistration(this, key, handler, synchronizationContext);

        lock (_gate)
        {
            if (_handlers.ContainsKey(key))
            {
                throw new InvalidOperationException("A handler is already registered for this path and interface.");
            }

            _handlers.Add(key, registration);
        }

        return registration;
    }

    /// <summary>
    /// Registers a <see cref="DBusObject"/> tree for service-side dispatch.
    /// </summary>
    public IDisposable RegisterObject(
        DBusObject root,
        SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var registration = new ObjectTreeRegistration(this, synchronizationContext);
        lock (_gate)
        {
            ThrowIfDisposed();
            RegisterSubtreeLocked(registration, root);
        }

        return registration;
    }

    /// <summary>
    /// Queries active tree nodes with a simple selector syntax.
    /// Supported selectors:
    /// - <c>/a/b</c>
    /// - <c>/a/*</c>
    /// - <c>/a/**</c>
    /// - predicate suffix: <c>[iface='org.example.Interface']</c>
    /// </summary>
    public IReadOnlyList<DBusObject> Query(string selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
            throw new ArgumentException("Selector must be provided.", nameof(selector));

        var parsed = ParseQuerySelector(selector);
        lock (_gate)
        {
            ThrowIfDisposed();

            IEnumerable<string> paths = parsed.Mode switch
            {
                QueryMode.Exact => _activePathRefCounts.ContainsKey(parsed.Path)
                    ? [parsed.Path]
                    : [],
                QueryMode.Children => QueryChildPathsLocked(parsed.Path),
                QueryMode.Descendants => QueryDescendantPathsLocked(parsed.Path),
                _ => Array.Empty<string>()
            };

            var results = new List<DBusObject>();
            foreach (var pathValue in paths.OrderBy(static x => x, StringComparer.Ordinal))
            {
                if (!_registeredObjects.TryGetValue(pathValue, out var registered))
                    continue;

                if (registered.Object is not DBusObject node)
                    continue;

                if (!MatchesInterfaceFilter(node, parsed.InterfaceFilter))
                    continue;

                results.Add(node);
            }

            return results;
        }
    }

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        ObjectTreeRegistration[] objectRegistrations;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            objectRegistrations = _registeredObjects.Values
                .Select(static x => x.Registration)
                .Where(static x => x != null)
                .Distinct()
                .Cast<ObjectTreeRegistration>()
                .ToArray();
        }

        foreach (var registration in objectRegistrations)
            registration.Dispose();

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
        {
            return;
        }

        var pathValue = (string)message.Path.Value;

        ObjectHandlerRegistration? registration;
        RegisteredObject? registeredObject;
        bool hasLegacyPath;
        bool hasObjectPath;
        var key = new ObjectHandlerKey(pathValue, message.Interface);
        lock (_gate)
        {
            _handlers.TryGetValue(key, out registration);
            hasLegacyPath = _handlers.Keys.Any(k => string.Equals(k.Path, pathValue, StringComparison.Ordinal));
            _registeredObjects.TryGetValue(pathValue, out registeredObject);
            hasObjectPath = _activePathRefCounts.ContainsKey(pathValue);
        }

        if (registration != null)
        {
            LogVerbose($"Dispatch METHOD_CALL: path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
            registration.Invoke(message);
            return;
        }

        if (hasObjectPath)
        {
            DispatchObjectCall(message, registeredObject);
            return;
        }

        ReplyMissingHandler(message, hasLegacyPath);
    }

    private void DispatchObjectCall(DBusMessage message, RegisteredObject? registeredObject)
    {
        LogVerbose($"Dispatch METHOD_CALL (object): path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
        if (registeredObject?.SynchronizationContext == null)
        {
            FireAndForget(HandleObjectCallAsync(message, registeredObject));
        }
        else
        {
            registeredObject.SynchronizationContext.Post(
                _ => FireAndForget(HandleObjectCallAsync(message, registeredObject)),
                null);
        }
    }

    private async Task HandleObjectCallAsync(DBusMessage message, RegisteredObject? registeredObject)
    {
        DBusMessage reply;
        try
        {
            var path = (string)message.Path!.Value;
            var objectForProperties = registeredObject?.Object ?? EmptyDbusObject.Instance;
            var introspectionXml = BuildIntrospectionXml(path, registeredObject?.Object);

            reply = _builtIns.TryHandlePeer(message)
                    ?? _builtIns.TryHandleProperties(message, objectForProperties)
                    ?? _builtIns.TryHandleIntrospectable(message, introspectionXml)
                    ?? await InvokeObjectMemberAsync(message, registeredObject);
        }
        catch (Exception ex)
        {
            reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
        }

        reply = EnsureReplyMetadata(message, reply);
        await Wire.SendAsync(reply);
    }

    private async Task<DBusMessage> InvokeObjectMemberAsync(DBusMessage message, RegisteredObject? registeredObject)
    {
        if (registeredObject == null)
        {
            var iface = string.IsNullOrWhiteSpace(message.Interface) ? "<null>" : message.Interface;
            var path = message.Path.HasValue ? (string)message.Path.Value : "<null>";
            return message.CreateError(
                "org.freedesktop.DBus.Error.UnknownInterface",
                $"No handler registered for interface '{iface}' on '{path}'.");
        }

        var reply = await registeredObject.Object.InvokeMember(message);
        return reply ?? message.CreateError("org.freedesktop.DBus.Error.Failed", "Handler returned null reply.");
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

    private IEnumerable<string> QueryDescendantPathsLocked(string basePath)
    {
        if (!_activePathRefCounts.ContainsKey(basePath))
            return [];

        return _activePathRefCounts.Keys.Where(path => IsDescendantPath(path, basePath));
    }

    private static bool IsDescendantPath(string path, string parentPath)
    {
        if (string.Equals(path, parentPath, StringComparison.Ordinal))
            return false;

        if (string.Equals(parentPath, "/", StringComparison.Ordinal))
            return path.StartsWith("/", StringComparison.Ordinal) && path.Length > 1;

        return path.Length > parentPath.Length
               && path.StartsWith(parentPath, StringComparison.Ordinal)
               && path[parentPath.Length] == '/';
    }

    private static bool MatchesInterfaceFilter(DBusObject node, string? interfaceFilter)
    {
        if (string.IsNullOrEmpty(interfaceFilter))
            return true;

        if (!node.TryGetInterfaces(out var ifaces))
            return false;

        foreach (var iface in ifaces)
        {
            if (string.Equals(iface, interfaceFilter, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static QuerySelector ParseQuerySelector(string selector)
    {
        var text = selector.Trim();
        var interfaceFilter = default(string);

        var predicateStart = text.IndexOf("[iface='", StringComparison.Ordinal);
        if (predicateStart >= 0)
        {
            const string predicatePrefix = "[iface='";
            const string predicateSuffix = "']";

            if (!text.EndsWith(predicateSuffix, StringComparison.Ordinal))
                throw new ArgumentException("Invalid selector predicate.", nameof(selector));

            var predicate = text[predicateStart..];
            if (!predicate.StartsWith(predicatePrefix, StringComparison.Ordinal))
                throw new ArgumentException("Only iface predicates are supported.", nameof(selector));

            var valueLength = predicate.Length - predicatePrefix.Length - predicateSuffix.Length;
            if (valueLength <= 0)
                throw new ArgumentException("Interface predicate value must be provided.", nameof(selector));

            interfaceFilter = predicate.Substring(predicatePrefix.Length, valueLength);
            text = text[..predicateStart];
        }

        if (text.EndsWith("/**", StringComparison.Ordinal))
        {
            var basePath = text[..^3];
            if (basePath.Length == 0)
                basePath = "/";
            basePath = NormalizePath(basePath);
            return new QuerySelector(basePath, QueryMode.Descendants, interfaceFilter);
        }

        if (text.EndsWith("/*", StringComparison.Ordinal))
        {
            var parentPath = text[..^2];
            if (parentPath.Length == 0)
                parentPath = "/";
            parentPath = NormalizePath(parentPath);
            return new QuerySelector(parentPath, QueryMode.Children, interfaceFilter);
        }

        if (text.Contains('*'))
            throw new ArgumentException("Unsupported wildcard selector.", nameof(selector));

        var exactPath = NormalizePath(text);
        return new QuerySelector(exactPath, QueryMode.Exact, interfaceFilter);
    }

    private void RegisterSubtreeLocked(ObjectTreeRegistration registration, DBusObject root)
    {
        foreach (var node in EnumerateSubtree(root))
        {
            RegisterNodeLocked(registration, node);
        }
    }

    private void UnregisterSubtreeLocked(ObjectTreeRegistration registration, DBusObject root)
    {
        var nodes = EnumerateSubtree(root);
        nodes.Sort(static (a, b) => StringComparer.Ordinal.Compare(b.Path, a.Path));
        foreach (var node in nodes)
        {
            UnregisterNodeLocked(registration, node);
        }
    }

    private void RegisterNodeLocked(ObjectTreeRegistration registration, DBusObject node)
    {
        var path = NormalizePath(node.Path);

        if (registration.TryGetNode(path, out var existingInRegistration))
        {
            if (!ReferenceEquals(existingInRegistration, node))
                throw new InvalidOperationException($"Multiple objects in the same tree use path '{path}'.");
            return;
        }

        if (_registeredObjects.TryGetValue(path, out var existing))
        {
            if (!ReferenceEquals(existing.Object, node))
                throw new InvalidOperationException($"An object is already registered for path '{path}'.");
            return;
        }

        registration.TrackNode(path, node);
        _registeredObjects[path] = new RegisteredObject(node, registration, registration.SynchronizationContext);
        IncrementPathRefCountsLocked(path);
    }

    private void UnregisterNodeLocked(ObjectTreeRegistration registration, DBusObject node)
    {
        var path = NormalizePath(node.Path);
        if (!registration.TryGetNode(path, out var existing) || !ReferenceEquals(existing, node))
            return;

        registration.UntrackNode(path, node);

        if (_registeredObjects.TryGetValue(path, out var current)
            && ReferenceEquals(current.Registration, registration)
            && ReferenceEquals(current.Object, node))
        {
            _registeredObjects.Remove(path);
            DecrementPathRefCountsLocked(path);
        }
    }

    private void UnregisterTreeLocked(ObjectTreeRegistration registration)
    {
        var nodes = registration.SnapshotNodes();
        Array.Sort(nodes, static (a, b) => StringComparer.Ordinal.Compare(b.Path, a.Path));

        foreach (var (path, node) in nodes)
        {
            registration.UntrackNode(path, node);

            if (_registeredObjects.TryGetValue(path, out var current)
                && ReferenceEquals(current.Registration, registration)
                && ReferenceEquals(current.Object, node))
            {
                _registeredObjects.Remove(path);
                DecrementPathRefCountsLocked(path);
            }
        }
    }

    private void OnTreeChildAdded(ObjectTreeRegistration registration, DBusObject parent, DBusObject child)
    {
        lock (_gate)
        {
            if (registration.IsDisposed || !registration.ContainsNode(parent))
                return;

            RegisterSubtreeLocked(registration, child);
        }
    }

    private void OnTreeChildRemoved(ObjectTreeRegistration registration, DBusObject child)
    {
        lock (_gate)
        {
            if (registration.IsDisposed)
                return;

            UnregisterSubtreeLocked(registration, child);
        }
    }

    private string BuildIntrospectionXml(string path, IDBusObject? obj)
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

        if (obj != null && obj.TryGetInterfaces(out var ifaces))
        {
            foreach (var iface in ifaces.OrderBy(static x => x, StringComparer.Ordinal))
            {
                if (!obj.TryGetIntrospectionXml(iface, out var xml))
                    continue;

                var ifaceRoot = xml.DocumentElement;
                if (ifaceRoot == null)
                    continue;

                var imported = doc.ImportNode(ifaceRoot, deep: true);
                root.AppendChild(imported);
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

    private static List<DBusObject> EnumerateSubtree(DBusObject root)
    {
        var result = new List<DBusObject>();
        var visited = new HashSet<DBusObject>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<DBusObject>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
                continue;

            result.Add(current);

            var children = current.Children;
            for (var i = children.Count - 1; i >= 0; i--)
            {
                stack.Push(children[i]);
            }
        }

        return result;
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
            return reply;

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
        {
            return string.Empty;
        }

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

    private enum QueryMode
    {
        Exact,
        Children,
        Descendants
    }

    private readonly struct QuerySelector(string path, QueryMode mode, string? interfaceFilter)
    {
        public string Path { get; } = path;
        public QueryMode Mode { get; } = mode;
        public string? InterfaceFilter { get; } = interfaceFilter;
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

    private sealed class RegisteredObject(IDBusObject obj, ObjectTreeRegistration? registration, SynchronizationContext? synchronizationContext)
    {
        public IDBusObject Object { get; } = obj;
        public ObjectTreeRegistration? Registration { get; } = registration;
        public SynchronizationContext? SynchronizationContext { get; } = synchronizationContext;
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

    private sealed class ObjectTreeRegistration(DBusConnection connection, SynchronizationContext? synchronizationContext) : IDisposable
    {
        private readonly Dictionary<string, DBusObject> _nodesByPath = new(StringComparer.Ordinal);
        private readonly Dictionary<DBusObject, NodeEventHandlers> _nodeEventHandlers = new(ReferenceEqualityComparer.Instance);
        private bool _disposed;

        public SynchronizationContext? SynchronizationContext { get; } = synchronizationContext;

        public bool IsDisposed => _disposed;

        public bool ContainsNode(DBusObject node) => _nodeEventHandlers.ContainsKey(node);

        public bool TryGetNode(string path, out DBusObject node) => _nodesByPath.TryGetValue(path, out node!);

        public void TrackNode(string path, DBusObject node)
        {
            if (_nodesByPath.ContainsKey(path))
                return;

            _nodesByPath[path] = node;

            EventHandler<DBusObjectChildChangedEventArgs> added = (_, args) =>
                connection.OnTreeChildAdded(this, node, args.Child);
            EventHandler<DBusObjectChildChangedEventArgs> removed = (_, args) =>
                connection.OnTreeChildRemoved(this, args.Child);

            node.ChildAdded += added;
            node.ChildRemoved += removed;

            _nodeEventHandlers[node] = new NodeEventHandlers(added, removed);
        }

        public void UntrackNode(string path, DBusObject node)
        {
            _nodesByPath.Remove(path);

            if (_nodeEventHandlers.TryGetValue(node, out var handlers))
            {
                node.ChildAdded -= handlers.ChildAdded;
                node.ChildRemoved -= handlers.ChildRemoved;
                _nodeEventHandlers.Remove(node);
            }
        }

        public (string Path, DBusObject Node)[] SnapshotNodes()
        {
            return _nodesByPath.Select(static pair => (pair.Key, pair.Value)).ToArray();
        }

        public void Dispose()
        {
            lock (connection._gate)
            {
                if (_disposed)
                    return;

                _disposed = true;
                connection.UnregisterTreeLocked(this);
            }
        }

        private sealed class NodeEventHandlers(
            EventHandler<DBusObjectChildChangedEventArgs> childAdded,
            EventHandler<DBusObjectChildChangedEventArgs> childRemoved)
        {
            public EventHandler<DBusObjectChildChangedEventArgs> ChildAdded { get; } = childAdded;
            public EventHandler<DBusObjectChildChangedEventArgs> ChildRemoved { get; } = childRemoved;
        }
    }

    private sealed class EmptyDbusObject : IDBusObject
    {
        public static EmptyDbusObject Instance { get; } = new();

        public DBusObjectPath Path => (DBusObjectPath)"/";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public Task<DBusMessage> InvokeMember(DBusMessage request)
        {
            return Task.FromResult(request.CreateError("org.freedesktop.DBus.Error.UnknownInterface", "Unknown interface"));
        }

        public bool TryGetAllProperties(string iface, out Dictionary<string, DBusVariant> props)
        {
            props = new Dictionary<string, DBusVariant>(StringComparer.Ordinal);
            return false;
        }

        public bool TryGetInterfaces(out IReadOnlyList<string> ifaces)
        {
            ifaces = Array.Empty<string>();
            return true;
        }

        public bool TryGetIntrospectionXml(string iface, out System.Xml.XmlDocument value)
        {
            value = null!;
            return false;
        }

        public bool TryGetProperty(string iface, string name, out DBusVariant value)
        {
            value = new DBusVariant(string.Empty);
            return false;
        }

        public bool TrySetProperty(string iface, string name, DBusVariant value)
        {
            return false;
        }
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
            {
                return;
            }

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
            {
                return;
            }

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
                {
                    reply = message.CreateError("org.freedesktop.DBus.Error.Failed", "Handler returned null reply.");
                }
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
