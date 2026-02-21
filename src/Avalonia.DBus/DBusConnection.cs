using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
sealed class DBusConnection : IDBusConnection
{
    private readonly ChannelWriter<object> _channel;

    private DBusConnection(DBusWireConnection wire, IDBusDiagnostics? diagnostics)
    {
        ArgumentNullException.ThrowIfNull(wire);

        var controlChannel = Channel.CreateUnbounded<object>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        _channel = controlChannel.Writer;

        var worker = new DBusConnectionWorker(
            this,
            wire,
            diagnostics,
            controlChannel.Reader,
            controlChannel.Writer);

        worker.Start();
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

        var token = new object();
        var completion = CreateCompletionSource<bool>();
        var targetArray = targets.ToArray();

        EnqueueOrThrowDisposed(new RegisterObjectsMessage(path, targetArray, synchronizationContext, token, completion));
        completion.Task.GetAwaiter().GetResult();

        return new RegistrationHandle(_channel, token);
    }

    public Task SendMessageAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var completion = CreateCompletionSource<bool>();

        return !_channel.TryWrite(new RawDBusMessageMessage(message, cancellationToken, completion)) ? 
            Task.FromException(new ObjectDisposedException(nameof(DBusConnection))) : completion.Task;
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
        var message = DBusMessage.CreateMethodCall(destination, path, iface, member, args);
        var completion = CreateCompletionSource<DBusMessage>();

        EnqueueOrThrowDisposed(new MethodCallMessage(message, completion, cancellationToken));

        var reply = await completion.Task;
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
        var token = new object();
        var completion = CreateCompletionSource<bool>();

        EnqueueOrThrowDisposed(
            new SubscribeMessage(
                sender,
                path,
                iface,
                member,
                handler,
                synchronizationContext,
                token,
                completion));

        await completion.Task;
        return new SubscriptionHandle(_channel, token);
    }

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        var completion = CreateCompletionSource<bool>();

        if (!_channel.TryWrite(new DisposeConnectionMessage(completion)))
            return;

        await completion.Task;
    }

    public async Task<string?> GetUniqueNameAsync()
    {
        var completion = CreateCompletionSource<string?>();
        EnqueueOrThrowDisposed(new GetUniqueNameMessage(completion));
        return await completion.Task;
    }

    private void EnqueueOrThrowDisposed(object message)
    {
        if (_channel.TryWrite(message)) return;
        throw new ObjectDisposedException(nameof(DBusConnection));
    }

    private static TaskCompletionSource<T> CreateCompletionSource<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void ThrowIfError(DBusMessage reply)
    {
        if (reply.Type != DBusMessageType.Error)
            return;

        var errorName = reply.ErrorName ?? "org.freedesktop.DBus.Error.Failed";
        string? errorMessage = null;

        if (reply.Body.Count > 0 && reply.Body[0] is string message)
            errorMessage = message;

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

    private static string BuildMatchRule(string? sender, DBusObjectPath? path, string iface, string member)
    {
        HashSet<string> parts = ["type='signal'"];

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

    private sealed class RegistrationHandle(ChannelWriter<object> channel, object token) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            channel.TryWrite(new UnRegisterObjectsMessage(token));
        }
    }

    private sealed class SubscriptionHandle(ChannelWriter<object> channel, object token) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            channel.TryWrite(new UnsubscribeMessage(token));
        }
    }

    private sealed record MethodCallMessage(
        DBusMessage Call,
        TaskCompletionSource<DBusMessage> ReplyTcs,
        CancellationToken CancellationToken);

    private sealed record RegisterObjectsMessage(
        DBusObjectPath Path,
        IReadOnlyList<object> Targets,
        SynchronizationContext? SynchronizationContext,
        object Token,
        TaskCompletionSource<bool> Completion);

    private sealed record UnRegisterObjectsMessage(object Token);

    private sealed record SubscribeMessage(
        string? Sender,
        DBusObjectPath? Path,
        string Interface,
        string Member,
        Func<DBusMessage, Task> Handler,
        SynchronizationContext? SynchronizationContext,
        object Token,
        TaskCompletionSource<bool> Completion);

    private sealed record UnsubscribeMessage(object Token);

    private sealed record RawDBusMessageMessage(
        DBusMessage Message,
        CancellationToken CancellationToken,
        TaskCompletionSource<bool>? Completion);

    private sealed record GetUniqueNameMessage(TaskCompletionSource<string?> Completion);

    private sealed record DisposeConnectionMessage(TaskCompletionSource<bool> Completion);

    private sealed record IncomingWireMessage(DBusMessage Message);

    private sealed class DBusConnectionWorker(
        IDBusConnection connection,
        DBusWireConnection wire,
        IDBusDiagnostics? diagnostics,
        ChannelReader<object> controlReader,
        ChannelWriter<object> controlWriter)
    {
        private readonly Dictionary<ObjectHandlerKey, HandlerRegistrationState> _handlers = new();
        private readonly Dictionary<object, RegistrationState> _registrations = new();
        private readonly Dictionary<object, SignalSubscriptionState> _subscriptions = new();

        private bool _disposed;

        public void Start()
        {
            var runTask = Task.Run(RunAsync);
            _ = runTask.ContinueWith(
                t =>
                {
                    if (t.Exception != null)
                        diagnostics?.OnUnobservedException(t.Exception);
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private async Task RunAsync()
        {
            var wireMessages = Select(
                wire.ReceivingReader,
                static object (message) => new IncomingWireMessage(message));

            var merged = Merge(controlReader, wireMessages);

            await foreach (var item in merged.ReadAllAsync())
            {
                if (item is IncomingWireMessage incoming)
                {
                    DispatchIncoming(incoming.Message);
                    continue;
                }

                await HandleControlMessageAsync(item);

                if (_disposed)
                    return;
            }
        }

        private async Task HandleControlMessageAsync(object item)
        {
            switch (item)
            {
                case MethodCallMessage methodCall:
                    try
                    {
                        ThrowIfDisposed();
                        FireAndForget(CompleteMethodCallAsync(methodCall));
                    }
                    catch (Exception ex)
                    {
                        methodCall.ReplyTcs.TrySetException(ex);
                    }

                    break;

                case RegisterObjectsMessage registerObjects:
                    try
                    {
                        ThrowIfDisposed();
                        RegisterObjects(registerObjects);
                        registerObjects.Completion.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        registerObjects.Completion.TrySetException(ex);
                    }

                    break;

                case UnRegisterObjectsMessage unregister:
                    UnregisterObjects(unregister.Token);
                    break;

                case SubscribeMessage subscribe:
                    try
                    {
                        ThrowIfDisposed();
                        await SubscribeAsync(subscribe);
                        subscribe.Completion.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        subscribe.Completion.TrySetException(ex);
                    }

                    break;

                case UnsubscribeMessage unsubscribe:
                    Unsubscribe(unsubscribe.Token);
                    break;

                case RawDBusMessageMessage raw:
                    try
                    {
                        ThrowIfDisposed();
                        await wire.SendAsync(raw.Message, raw.CancellationToken);
                        raw.Completion?.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        if (raw.Completion != null)
                            raw.Completion.TrySetException(ex);
                        else
                            diagnostics?.OnUnobservedException(ex);
                    }

                    break;

                case GetUniqueNameMessage getUniqueName:
                    try
                    {
                        ThrowIfDisposed();
                        var name = await wire.GetUniqueNameAsync();
                        getUniqueName.Completion.TrySetResult(name);
                    }
                    catch (Exception ex)
                    {
                        getUniqueName.Completion.TrySetException(ex);
                    }

                    break;

                case DisposeConnectionMessage dispose:
                    await DisposeAsync(dispose);
                    break;
            }
        }

        private void RegisterObjects(RegisterObjectsMessage request)
        {
            if (_registrations.ContainsKey(request.Token))
                throw new InvalidOperationException("Registration token already exists.");

            if (request.Targets.Count == 0)
                throw new InvalidOperationException("At least one target is required.");

            var normalizedPath = NormalizePath(request.Path.Value);
            List<(object Target, DBusInteropMetadata Registration)> registrations = [];

            foreach (var target in request.Targets)
            {
                ArgumentNullException.ThrowIfNull(target);

                var targetRegistrations =
                    DBusInteropMetadataRegistry.ResolveHandlerRegistrations(target.GetType());
                if (targetRegistrations.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"No generated handler registration exists for CLR type '{target.GetType().FullName}'.");
                }

                registrations.AddRange(targetRegistrations.Select(t => (target, t)));
            }

            List<ObjectHandlerKey> keysAdded = [];
            var boundPropertiesByInterface = new Dictionary<string, BoundProperties>(StringComparer.Ordinal);

            try
            {
                foreach (var (target, registration) in registrations)
                {
                    var createHandler = registration.CreateHandler
                                         ?? throw new InvalidOperationException(
                                             $"Generated handler registration for " +
                                             $"'{registration.InterfaceName}' is missing CreateHandler delegate.");

                    if (string.IsNullOrEmpty(registration.InterfaceName))
                        throw new ArgumentException("Interface is required.", nameof(registration.InterfaceName));

                    var key = new ObjectHandlerKey(normalizedPath, registration.InterfaceName);

                    if (!_handlers.TryAdd(
                            key,
                            new HandlerRegistrationState(
                                connection,
                                target,
                                createHandler(),
                                request.SynchronizationContext,
                                controlWriter,
                                diagnostics)))
                    {
                        throw new InvalidOperationException(
                            "A handler is already registered for this path and interface.");
                    }

                    keysAdded.Add(key);

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
                                        var values = 
                                            registration.GetAllPropertiesFactory(target);
                                        return values.GetValueOrDefault(propertyName);
                                    },
                                registration.TrySetProperty == null
                                    ? null
                                    : (propertyName, value) => 
                                        registration.TrySetProperty(target, propertyName, value),
                                registration.GetAllPropertiesFactory == null
                                    ? null
                                    : () => registration.GetAllPropertiesFactory(target))))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate generated handler registration for interface " +
                            $"'{registration.InterfaceName}'.");
                    }
                }

                if (boundPropertiesByInterface.Count > 0)
                {
                    var key = new ObjectHandlerKey(normalizedPath, BuiltInPropertiesHandler.InterfaceName);
                    if (!_handlers.TryAdd(
                            key,
                            new HandlerRegistrationState(
                                connection,
                                target: null,
                                new BuiltInPropertiesHandler(boundPropertiesByInterface),
                                request.SynchronizationContext,
                                controlWriter,
                                diagnostics)))
                    {
                        throw new InvalidOperationException(
                            "A handler is already registered for this path and interface.");
                    }

                    keysAdded.Add(key);
                }

                var introspectionKey = 
                    new ObjectHandlerKey(normalizedPath, BuiltInIntrospectionHandler.InterfaceName);
                if (!_handlers.ContainsKey(introspectionKey))
                {
                    var introspectionHandler = new BuiltInIntrospectionHandler(ResolveIntrospectionData)
                    {
                        BoundPath = normalizedPath
                    };

                    _handlers.Add(
                        introspectionKey,
                        new HandlerRegistrationState(
                            connection,
                            target: null,
                            introspectionHandler,
                            context: null,
                            controlWriter,
                            diagnostics));

                    keysAdded.Add(introspectionKey);
                }

                _registrations.Add(request.Token, new RegistrationState(keysAdded));
            }
            catch
            {
                foreach (var key in keysAdded)
                    _handlers.Remove(key);

                throw;
            }
        }

        private void UnregisterObjects(object token)
        {
            if (!_registrations.TryGetValue(token, out var registration))
                return;

            foreach (var key in registration.Keys)
                _handlers.Remove(key);

            _registrations.Remove(token);
        }

        private async Task SubscribeAsync(SubscribeMessage request)
        {
            if (_subscriptions.ContainsKey(request.Token))
                throw new InvalidOperationException("Subscription token already exists.");

            if (string.IsNullOrEmpty(request.Interface))
                throw new ArgumentException("Interface is required.", nameof(request.Interface));

            if (string.IsNullOrEmpty(request.Member))
                throw new ArgumentException("Member is required.", nameof(request.Member));

            ArgumentNullException.ThrowIfNull(request.Handler);

            var matchRule = 
                BuildMatchRule(request.Sender, request.Path, request.Interface, request.Member);
            await AddMatchAsync(matchRule);

            _subscriptions.Add(
                request.Token,
                new SignalSubscriptionState(
                    request.Sender,
                    request.Path,
                    request.Interface,
                    request.Member,
                    request.Handler,
                    request.SynchronizationContext,
                    matchRule,
                    diagnostics));
        }

        private void Unsubscribe(object token)
        {
            if (!_subscriptions.Remove(token, out var subscription))
                return;

            FireAndForget(RemoveMatchAsync(subscription.MatchRule));
        }

        private void DispatchIncoming(DBusMessage message)
        {
            if (_disposed)
                return;

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

        private void DispatchSignal(DBusMessage message)
        {
            foreach (var subscription in _subscriptions.Values
                         .Where(subscription => subscription.IsMatch(message)))
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

            var key = new ObjectHandlerKey(pathValue, message.Interface);
            if (_handlers.TryGetValue(key, out var registration))
            {
                registration.Invoke(message);
                return;
            }

            var hasPath = _handlers.Keys
                .Any(k => string.Equals(k.Path, pathValue, StringComparison.Ordinal));

            if (string.Equals(message.Interface, 
                    BuiltInIntrospectionHandler.InterfaceName, StringComparison.Ordinal)
                && string.Equals(message.Member, "Introspect", StringComparison.Ordinal))
            {
                HandleVirtualIntrospection(message, pathValue);
                return;
            }

            ReplyMissingHandler(message, hasPath);
        }

        private void ReplyMissingHandler(DBusMessage message, bool hasPath)
        {
            if ((message.Flags & DBusMessageFlags.NoReplyExpected) != 0)
                return;

            var iface = string.IsNullOrWhiteSpace(message.Interface) ? "<null>" : message.Interface;
            var path = message.Path ?? "<null>";

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

            var error = message.CreateError(errorName, errorMessage);
            controlWriter.TryWrite(new RawDBusMessageMessage(error, CancellationToken.None, Completion: null));
        }

        private void HandleVirtualIntrospection(DBusMessage message, string pathValue)
        {
            var data = ResolveIntrospectionData(pathValue);
            if (data.ChildSegments.Count == 0 && data.Interfaces.Count == 0)
            {
                ReplyMissingHandler(message, hasPath: false);
                return;
            }

            var handler = new BuiltInIntrospectionHandler(_ => data)
            {
                BoundPath = pathValue
            };

            var reply = handler.Handle(connection, null, message).GetAwaiter().GetResult();
            var replyWithMetadata = EnsureReplyMetadata(message, reply);
            controlWriter.TryWrite(new RawDBusMessageMessage(replyWithMetadata, CancellationToken.None, Completion: null));
        }

        private IntrospectionData ResolveIntrospectionData(string path)
        {
            var interfaces = _handlers.Keys
                .Where(k => string.Equals(k.Path, path, StringComparison.Ordinal)
                            && !string.Equals(k.Iface, BuiltInPropertiesHandler.InterfaceName, StringComparison.Ordinal)
                            && !string.Equals(k.Iface, BuiltInIntrospectionHandler.InterfaceName, StringComparison.Ordinal))
                .Select(k => k.Iface)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(i => i, StringComparer.Ordinal)
                .Select(iface => (iface, DBusInteropMetadataRegistry.GetIntrospectionWriter(iface)))
                .ToList();

            var prefix = path == "/" ? "/" : path + "/";
            
            var childSegments = new HashSet<string>(StringComparer.Ordinal);
            
            foreach (var segment in  // This seems easier to follow than the method chain/fluent version.
                     from key in _handlers.Keys
                     where key.Path.StartsWith(prefix, StringComparison.Ordinal) 
                     select key.Path[prefix.Length..] 
                     into remainder 
                     let slashIndex = remainder.IndexOf('/') 
                     select slashIndex >= 0 ? remainder[..slashIndex] :
                         remainder into segment 
                     where !string.IsNullOrEmpty(segment) 
                     select segment)
            {
                childSegments.Add(segment);
            }

            return new IntrospectionData
            {
                Interfaces = interfaces,
                ChildSegments = childSegments.OrderBy(s => s, StringComparer.Ordinal).ToHashSet()
            };
        }

        private async Task CallBusMethodAsync(
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

            var reply = await wire.SendWithReplyAsync(message, cancellationToken);
            ThrowIfError(reply);
        }

        private async Task CompleteMethodCallAsync(MethodCallMessage methodCall)
        {
            try
            {
                var reply = await wire.SendWithReplyAsync(methodCall.Call, methodCall.CancellationToken);
                methodCall.ReplyTcs.TrySetResult(reply);
            }
            catch (Exception ex)
            {
                methodCall.ReplyTcs.TrySetException(ex);
            }
        }

        private async Task AddMatchAsync(string rule)
        {
            await CallBusMethodAsync("AddMatch", CancellationToken.None, rule);
        }

        private async Task RemoveMatchAsync(string rule)
        {
            await CallBusMethodAsync("RemoveMatch", CancellationToken.None, rule);
        }

        private async Task DisposeAsync(DisposeConnectionMessage request)
        {
            if (_disposed)
            {
                request.Completion.TrySetResult(true);
                return;
            }

            _disposed = true;
            _handlers.Clear();
            _registrations.Clear();
            _subscriptions.Clear();

            Exception? disposeError = null;
            try
            {
                await wire.DisposeAsync();
            }
            catch (Exception ex)
            {
                disposeError = ex;
            }

            var pendingError = new ObjectDisposedException(nameof(DBusConnection));
            controlWriter.TryComplete(disposeError);
            DrainControlQueue(pendingError);

            if (disposeError != null)
                request.Completion.TrySetException(disposeError);
            else
                request.Completion.TrySetResult(true);
        }

        private void DrainControlQueue(Exception error)
        {
            while (controlReader.TryRead(out var pending))
                FailControlMessage(pending, error);
        }

        private void FailControlMessage(object message, Exception error)
        {
            switch (message)
            {
                case MethodCallMessage methodCall:
                    methodCall.ReplyTcs.TrySetException(error);
                    break;
                case RegisterObjectsMessage register:
                    register.Completion.TrySetException(error);
                    break;
                case SubscribeMessage subscribe:
                    subscribe.Completion.TrySetException(error);
                    break;
                case RawDBusMessageMessage { Completion: { } completion }:
                    completion.TrySetException(error);
                    break;
                case GetUniqueNameMessage getUniqueName:
                    getUniqueName.Completion.TrySetException(error);
                    break;
                case DisposeConnectionMessage dispose:
                    dispose.Completion.TrySetException(error);
                    break;
            }
        }

        private void ThrowIfDisposed()
        {
            if (!_disposed) return;
            throw new ObjectDisposedException(nameof(DBusConnection));
        }

        private void FireAndForget(Task task)
        {
            _ = task.ContinueWith(
                t =>
                {
                    if (t.Exception != null)
                        diagnostics?.OnUnobservedException(t.Exception);
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        private static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
        {
            var output = Channel.CreateUnbounded<T>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            _ = Task.Run(async () =>
            {
                try
                {
                    var tasks = inputs.Select(async input =>
                    {
                        await foreach (var item in input.ReadAllAsync())
                            await output.Writer.WriteAsync(item);
                    });

                    await Task.WhenAll(tasks);
                    output.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    output.Writer.TryComplete(ex);
                }
            });

            return output.Reader;
        }

        private static ChannelReader<TOutput> Select<TInput, TOutput>(
            ChannelReader<TInput> input,
            Func<TInput, TOutput> selector)
        {
            var output = Channel.CreateUnbounded<TOutput>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = true
                });

            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in input.ReadAllAsync())
                        await output.Writer.WriteAsync(selector(item));

                    output.Writer.TryComplete();
                }
                catch (Exception ex)
                {
                    output.Writer.TryComplete(ex);
                }
            });

            return output.Reader;
        }

        private sealed class RegistrationState(IReadOnlyList<ObjectHandlerKey> keys)
        {
            public IReadOnlyList<ObjectHandlerKey> Keys { get; } = keys;
        }

        private sealed class HandlerRegistrationState(
            IDBusConnection connection,
            object? target,
            IDBusInterfaceCallDispatcher dispatcher,
            SynchronizationContext? context,
            ChannelWriter<object> controlWriter,
            IDBusDiagnostics? diagnostics)
        {
            public void Invoke(DBusMessage message)
            {
                if (context == null)
                {
                    FireAndForget(HandleAsync(message));
                }
                else
                {
                    context.Post(
                        static state =>
                        {
                            if (state is HandlerInvocationState invocation)
                                invocation.Registration
                                    .FireAndForget(invocation.Registration.HandleAsync(invocation.Message));
                        },
                        new HandlerInvocationState(this, message));
                }
            }

            private async Task HandleAsync(DBusMessage message)
            {
                DBusMessage? reply;
                try
                {
                    reply = await dispatcher.Handle(connection, target, message);
                }
                catch (DBusException dbusEx)
                {
                    reply = message.CreateError(dbusEx.ErrorName, dbusEx.Message);
                }
                catch (Exception ex)
                {
                    diagnostics?.OnUnobservedException(ex);
                    reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
                }

                var replyWithMetadata = EnsureReplyMetadata(message, reply);
                controlWriter.TryWrite(new RawDBusMessageMessage(replyWithMetadata, CancellationToken.None, Completion: null));
            }

            private void FireAndForget(Task task)
            {
                _ = task.ContinueWith(
                    t =>
                    {
                        if (t.Exception != null)
                            diagnostics?.OnUnobservedException(t.Exception);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            private readonly record struct HandlerInvocationState(
                HandlerRegistrationState Registration,
                DBusMessage Message);
        }

        private sealed class SignalSubscriptionState(
            string? sender,
            DBusObjectPath? path,
            string iface,
            string member,
            Func<DBusMessage, Task> handler,
            SynchronizationContext? context,
            string matchRule,
            IDBusDiagnostics? diagnostics)
        {
            public string MatchRule { get; } = matchRule;

            public bool IsMatch(DBusMessage message)
            {
                if (message.Type != DBusMessageType.Signal)
                    return false;

                if (!string.IsNullOrEmpty(sender) && 
                    !string.Equals(message.Sender, sender, StringComparison.Ordinal))
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
                if (context == null)
                {
                    FireAndForget(InvokeHandlerAsync(message));
                }
                else
                {
                    context.Post(
                        static state =>
                        {
                            if (state is SubscriptionInvocationState invocation)
                                invocation.Subscription.FireAndForget(invocation.Subscription.InvokeHandlerAsync(invocation.Message));
                        },
                        new SubscriptionInvocationState(this, message));
                }
            }

            private async Task InvokeHandlerAsync(DBusMessage message)
            {
                try
                {
                    await handler(message);
                }
                catch (Exception ex)
                {
                    diagnostics?.OnUnobservedException(ex);
                }
            }

            private void FireAndForget(Task task)
            {
                _ = task.ContinueWith(
                    t =>
                    {
                        if (t.Exception != null)
                            diagnostics?.OnUnobservedException(t.Exception);
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }

            private readonly record struct SubscriptionInvocationState(
                SignalSubscriptionState Subscription,
                DBusMessage Message);
        }
    }
}
