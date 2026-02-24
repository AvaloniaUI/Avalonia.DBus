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
sealed partial class DBusConnection
{
    private sealed class DBusConnectionWorker(
        IDBusConnection connection,
        IDBusWireConnection wire,
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
            var wireMessages = ChannelExtensions.Select(
                wire.ReceivingReader,
                static object (message) => new IncomingWireMessage(message));

            var merged = ChannelExtensions.Merge(controlReader, wireMessages);

            await foreach (var item in merged.ReadAllAsync())
            {
                if (item is IncomingWireMessage incoming)
                {
                    await DispatchIncomingAsync(incoming.Message);
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
            if (!wire.IsPeerToPeer)
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

            if (!wire.IsPeerToPeer)
                FireAndForget(RemoveMatchAsync(subscription.MatchRule));
        }

        private async Task DispatchIncomingAsync(DBusMessage message)
        {
            if (_disposed)
                return;

            switch (message)
            {
                case { Type: DBusMessageType.Signal }:
                    DispatchSignal(message);
                    break;
                case { Type: DBusMessageType.MethodCall }:
                    await DispatchMethodCallAsync(message);
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

        private async Task DispatchMethodCallAsync(DBusMessage message)
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
                await HandleVirtualIntrospectionAsync(message, pathValue);
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

        private async Task HandleVirtualIntrospectionAsync(DBusMessage message, string pathValue)
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

            var reply = await handler.Handle(connection, null, message);
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

            var childSegments = new SortedSet<string>(StringComparer.Ordinal);

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
                ChildSegments = childSegments
            };
        }

        private async Task CallBusMethodAsync(
            string member,
            CancellationToken cancellationToken,
            params object[] body)
        {
            var message = DBusMessage.CreateMethodCall(
                "org.freedesktop.DBus",
                "/org/freedesktop/DBus",
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
