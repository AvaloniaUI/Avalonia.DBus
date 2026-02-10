using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public sealed class DBusConnection : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<ObjectHandlerKey, ObjectHandlerRegistration> _handlers = new();
    private readonly List<SignalSubscription> _subscriptions = [];
    private readonly CancellationTokenSource _dispatchCts = new();
    private readonly Task _dispatchLoop;
    private bool _disposed;

    private DBusConnection(DBusWireConnection wire)
    {
        Wire = wire ?? throw new ArgumentNullException(nameof(wire));
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
        return new DBusConnection(wire);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(cancellationToken);
        return new DBusConnection(wire);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(cancellationToken);
        return new DBusConnection(wire);
    }

    /// <summary>
    /// The underlying DBus wire connection.
    /// </summary>
    private DBusWireConnection Wire { get; }

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
        Func<DBusMessage, Task<DBusMessage>> handler,
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
    /// Closes the connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
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
        {
            return;
        }

        var pathValue = message.Path.Value.Value;

        ObjectHandlerRegistration? registration;
        var key = new ObjectHandlerKey(pathValue, message.Interface);
        lock (_gate)
        {
            _handlers.TryGetValue(key, out registration);
        }

        if (registration == null)
        {
            var iface = string.IsNullOrWhiteSpace(message.Interface) ? "<null>" : message.Interface;
            var member = string.IsNullOrWhiteSpace(message.Member) ? "<null>" : message.Member;
            var path = message.Path.HasValue ? message.Path.Value.Value : "<null>";
            bool hasPath;
            lock (_gate)
            {
                hasPath = _handlers.Keys.Any(k => string.Equals(k.Path, path, StringComparison.Ordinal));
            }

            if ((message.Flags & DBusMessageFlags.NoReplyExpected) == 0)
            {
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
            return;
        }

        LogVerbose($"Dispatch METHOD_CALL: path='{message.Path}' iface='{message.Interface}' member='{message.Member}'");
        registration.Invoke(message);
    }

    private static void LogVerbose(string message)
    {
#if DEBUG
        Console.Error.WriteLine($"[DBusConnection {Environment.CurrentManagedThreadId}] {message}");
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

    private static void ThrowIfError(DBusMessage reply)
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
            parts.Add($"path='{EscapeMatchValue(path.Value.Value)}'");

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
        Func<DBusMessage, Task<DBusMessage>> handler,
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
                reply = await handler(message);
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
