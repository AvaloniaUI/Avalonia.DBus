using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public sealed class DBusConnection : IDBusConnection
{
    private readonly object _gate = new();
    private readonly Dictionary<ObjectHandlerKey, ObjectHandlerRegistration> _handlers = new();
    private readonly List<SignalSubscription> _subscriptions = [];
    private readonly CancellationTokenSource _dispatchCts = new();
    private readonly Task _dispatchLoop;
    private readonly IDBusDiagnostics? _diagnostics;
    private bool _disposed;

    private DBusConnection(DBusWireConnection wire, IDBusDiagnostics? diagnostics)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
        _diagnostics = diagnostics;
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
        return new DBusConnection(wire, diagnostics: null);
    }

    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    public static async Task<DBusConnection> ConnectAsync(
        string address,
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectAsync(address, diagnostics, cancellationToken);
        return new DBusConnection(wire, diagnostics);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(cancellationToken);
        return new DBusConnection(wire, diagnostics: null);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(diagnostics, cancellationToken);
        return new DBusConnection(wire, diagnostics);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(cancellationToken);
        return new DBusConnection(wire, diagnostics: null);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(diagnostics, cancellationToken);
        return new DBusConnection(wire, diagnostics);
    }

    /// <summary>
    /// The underlying DBus wire connection.
    /// </summary>
    private DBusWireConnection Wire { get; }

    public object CreateProxy(
        Type interfaceType,
        string destination,
        DBusObjectPath path,
        string? iface = null)
    {
        return DBusInteropMetadataRegistry.CreateProxy(interfaceType, this, destination, path, iface);
    }

    /// <summary>
    /// Registers all generated D-Bus interfaces implemented by the provided <paramref name="targets"/> at the specified object path.
    /// </summary>
    public IDisposable RegisterObjects(
        DBusObjectPath path,
        IEnumerable<object> targets,
        SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var targetArray = targets.ToArray();
        if (targetArray.Length == 0)
            throw new InvalidOperationException("At least one target is required.");

        List<(object Target, DBusInteropMetadata Registration)> registrations = [];
        foreach (var target in targetArray)
        {
            ArgumentNullException.ThrowIfNull(target);

            var targetRegistrations = DBusInteropMetadataRegistry.ResolveHandlerRegistrations(target.GetType());
            if (targetRegistrations.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No generated handler registration exists for CLR type '{target.GetType().FullName}'.");
            }

            registrations.AddRange(targetRegistrations.Select(t => (target, t)));
        }

        List<IDisposable> handles = [];
        var boundPropertiesByInterface = new Dictionary<string, BoundProperties>(StringComparer.Ordinal);

        try
        {
            foreach (var (target, registration) in registrations)
            {
                var createHandler = registration.CreateHandler
                                     ?? throw new InvalidOperationException(
                                         $"Generated handler registration for '{registration.InterfaceName}' is missing CreateHandler delegate.");
                var dispatcher = createHandler();
                if (string.IsNullOrEmpty(registration.InterfaceName))
                    throw new ArgumentException("Interface is required.", nameof(registration.InterfaceName));

                var normalizedPath = NormalizePath(path.Value);
                var key = new ObjectHandlerKey(normalizedPath, registration.InterfaceName);
                var registrationHandle = new ObjectHandlerRegistration(
                    _gate,
                    _handlers,
                    EnsureReplyMetadata,
                    reply => Wire.SendAsync(reply),
                    FireAndForget,
                    this,
                    key,
                    target,
                    dispatcher,
                    synchronizationContext,
                    _diagnostics);

                lock (_gate)
                {
                    ThrowIfDisposed();

                    if (!_handlers.TryAdd(key, registrationHandle))
                    {
                        throw new InvalidOperationException("A handler is already registered for this path and interface.");
                    }
                }

                handles.Add(registrationHandle);

                if (registration.TrySetProperty == null
                    && registration.GetAllPropertiesFactory == null)
                {
                    continue;
                }

                if (!boundPropertiesByInterface.TryAdd(
                        registration.InterfaceName,
                        new BoundProperties(
                            registration.GetAllPropertiesFactory == null
                                ? null
                                : propertyName =>
                                {
                                    var values = registration.GetAllPropertiesFactory(target);
                                    return values.GetValueOrDefault(propertyName);
                                },
                            registration.TrySetProperty == null
                                ? null
                                : (propertyName, value) => registration.TrySetProperty(target, propertyName, value),
                            registration.GetAllPropertiesFactory == null
                                ? null
                                : () => registration.GetAllPropertiesFactory(target))))
                {
                    throw new InvalidOperationException(
                        $"Duplicate generated handler registration for interface '{registration.InterfaceName}'.");
                }
            }

            if (boundPropertiesByInterface.Count > 0)
            {
                var propertiesHandler = new BuiltInPropertiesHandler(boundPropertiesByInterface);
                var normalizedPath = NormalizePath(path.Value);
                var key = new ObjectHandlerKey(normalizedPath, BuiltInPropertiesHandler.InterfaceName);
                var registrationHandle = new ObjectHandlerRegistration(
                    _gate,
                    _handlers,
                    EnsureReplyMetadata,
                    reply => Wire.SendAsync(reply),
                    FireAndForget,
                    this,
                    key,
                    target: null,
                    propertiesHandler,
                    synchronizationContext,
                    _diagnostics);

                lock (_gate)
                {
                    ThrowIfDisposed();

                    if (!_handlers.TryAdd(key, registrationHandle))
                    {
                        throw new InvalidOperationException("A handler is already registered for this path and interface.");
                    }
                }

                handles.Add(registrationHandle);
            }

            return new CompositeDisposable(handles);
        }
        catch
        {
            foreach (var t in handles)
                t.Dispose();
            throw;
        }
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

        var subscription = new SignalSubscription(
            _gate,
            _subscriptions,
            RemoveMatch,
            FireAndForget,
            sender,
            path,
            iface,
            member,
            handler,
            synchronizationContext,
            matchRule,
            _diagnostics);
        lock (_gate)
        {
            ThrowIfDisposed();
            _subscriptions.Add(subscription);
        }

        return subscription;
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
            switch (message)
            {
                case { Type: DBusMessageType.Signal }:
                    DispatchSignal(message);
                    break;
                case { Type: DBusMessageType.MethodCall }:
                    DispatchMethodCall(message);
                    break;
            }
        }
    }

    private void DispatchSignal(DBusMessage message)
    {
        LogVerbose($"Dispatch SIGNAL: sender='{message.Sender}' path='{message.Path}' iface='{message.Interface}' member='{message.Member}' body={message.Body.Count}");

        List<SignalSubscription> snapshot;
        lock (_gate)
        {
            snapshot = _subscriptions.ToList();
        }

        foreach (var subscription in snapshot.Where(subscription => subscription.IsMatch(message)))
        {
            subscription.Invoke(message);
        }
    }

    private void DispatchMethodCall(DBusMessage message)
    {
        if (!message.Path.HasValue || string.IsNullOrEmpty(message.Interface))
            return;

        string pathValue;
        try
        {
            pathValue = NormalizePath(message.Path.Value);
        }
        catch
        {
            return;
        }

        ObjectHandlerRegistration? registration;
        bool hasPath;
        var key = new ObjectHandlerKey(pathValue, message.Interface);
        lock (_gate)
        {
            _handlers.TryGetValue(key, out registration);
            hasPath = _handlers.Keys.Any(k => string.Equals(k.Path, pathValue, StringComparison.Ordinal));
        }

        if (registration != null)
        {
            LogVerbose($"Dispatch METHOD_CALL: path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
            registration.Invoke(message);
            return;
        }

        ReplyMissingHandler(message, hasPath);
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DBusConnection));
    }

    private void LogVerbose(string message)
    {
        _diagnostics?.Log(DBusLogLevel.Verbose, message);
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

    private void FireAndForget(Task task)
    {
        _ = task.ContinueWith(
            t =>
            {
                if (t.Exception != null)
                    _diagnostics?.OnUnobservedException(t.Exception);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    public async Task<string?> GetUniqueNameAsync()
    {
        return await Wire.GetUniqueNameAsync();
    }
}
