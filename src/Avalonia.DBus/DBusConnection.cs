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
sealed partial class DBusConnection : IDBusConnection
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
        var wire = await DBusWireConnection.ConnectAsync(address, cancellationToken).ConfigureAwait(false);
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
        var wire = await DBusWireConnection.ConnectAsync(address, diagnostics, cancellationToken)
            .ConfigureAwait(false);
        return new DBusConnection(wire, diagnostics);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(cancellationToken).ConfigureAwait(false);
        return new DBusConnection(wire, diagnostics: null);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSessionAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSessionAsync(diagnostics, cancellationToken)
            .ConfigureAwait(false);
        return new DBusConnection(wire, diagnostics);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(cancellationToken).ConfigureAwait(false);
        return new DBusConnection(wire, diagnostics: null);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static async Task<DBusConnection> ConnectSystemAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        var wire = await DBusWireConnection.ConnectSystemAsync(diagnostics, cancellationToken)
            .ConfigureAwait(false);
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
    public async Task<IDisposable> RegisterObjects(
        DBusObjectPath path,
        IEnumerable<object> targets,
        SynchronizationContext? synchronizationContext = null)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var token = new object();
        var completion = CreateCompletionSource<bool>();
        var targetArray = targets.ToArray();

        EnqueueOrThrowDisposed(new RegisterObjectsMessage(path, targetArray, synchronizationContext, token, completion));
        await completion.Task.ConfigureAwait(false);

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

        var reply = await completion.Task.ConfigureAwait(false);
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

        await completion.Task.ConfigureAwait(false);
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

        await completion.Task.ConfigureAwait(false);
    }

    public async Task<string?> GetUniqueNameAsync()
    {
        var completion = CreateCompletionSource<string?>();
        EnqueueOrThrowDisposed(new GetUniqueNameMessage(completion));
        return await completion.Task.ConfigureAwait(false);
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
}
