using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using global::Tmds.DBus.Protocol;

namespace Avalonia.DBus.Tmds;

/// <summary>
/// An <see cref="IDBusWireConnection"/> implementation backed by
/// <see cref="Tmds.DBus.Protocol.DBusConnection"/>.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
sealed class TmdsDBusWireConnection : IDBusWireConnection, IPathMethodHandler
{
    private readonly global::Tmds.DBus.Protocol.DBusConnection _tmdsConnection;
    private readonly Channel<DBusMessage> _receiving;
    private readonly ConcurrentDictionary<uint, MethodContext> _pendingMethodCalls = new();
    private IDisposable? _matchDisposable;
    private int _disposed;

    private TmdsDBusWireConnection(global::Tmds.DBus.Protocol.DBusConnection connection)
    {
        _tmdsConnection = connection ?? throw new ArgumentNullException(nameof(connection));
        _receiving = Channel.CreateUnbounded<DBusMessage>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    /// <summary>
    /// Gets the channel reader for incoming messages (signals and method calls).
    /// </summary>
    public ChannelReader<DBusMessage> ReceivingReader => _receiving.Reader;

    /// <summary>
    /// This connection delegates to a bus, so it is never peer-to-peer.
    /// </summary>
    public bool IsPeerToPeer => false;

    // --- IPathMethodHandler ---

    string IPathMethodHandler.Path => "/";

    bool IPathMethodHandler.HandlesChildPaths => true;

    ValueTask IPathMethodHandler.HandleMethodAsync(MethodContext context)
    {
        context.DisposesAsynchronously = true;

        try
        {
            var msg = TmdsTypeConverter.ToDBusMessage(context.Request);

            // Store the MethodContext so we can route replies back
            _pendingMethodCalls[msg.Serial] = context;

            // Post to the receiving channel
            if (!_receiving.Writer.TryWrite(msg))
            {
                // Channel is full or closed – reply with error and clean up
                _pendingMethodCalls.TryRemove(msg.Serial, out _);
                context.ReplyError("org.freedesktop.DBus.Error.Failed", "Internal channel full");
                context.Dispose();
            }
        }
        catch (Exception)
        {
            context.ReplyError("org.freedesktop.DBus.Error.Failed", "Failed to convert incoming method call");
            context.Dispose();
        }

        return default;
    }

    // --- IDBusWireConnection ---

    /// <summary>
    /// Sends a message without waiting for a reply.
    /// For method returns/errors, routes them back to the stored <see cref="MethodContext"/>
    /// if one exists for the reply serial.
    /// </summary>
    public Task SendAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        // If this is a reply to an incoming method call, route it to the stored MethodContext
        if (message.Type is DBusMessageType.MethodReturn or DBusMessageType.Error
            && message.ReplySerial != 0
            && _pendingMethodCalls.TryRemove(message.ReplySerial, out var methodContext))
        {
            try
            {
                if (message.Type == DBusMessageType.Error)
                {
                    var errorMsg = message.Body.Count > 0 && message.Body[0] is string s ? s : null;
                    methodContext.ReplyError(message.ErrorName, errorMsg);
                }
                else
                {
                    var replyBuffer = TmdsTypeConverter.ToMessageBuffer(message, _tmdsConnection);
                    methodContext.Reply(replyBuffer);
                }
            }
            finally
            {
                methodContext.Dispose();
            }

            return Task.CompletedTask;
        }

        // Otherwise, just send it out via Tmds
        var buffer = TmdsTypeConverter.ToMessageBuffer(message, _tmdsConnection);
        _tmdsConnection.TrySendMessage(buffer);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    public async Task<DBusMessage> SendWithReplyAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var buffer = TmdsTypeConverter.ToMessageBuffer(message, _tmdsConnection);

        var reply = await _tmdsConnection.CallMethodAsync<DBusMessage>(
            buffer,
            static (msg, _) => TmdsTypeConverter.ToDBusMessage(msg),
            readerState: null).ConfigureAwait(false);

        return reply;
    }

    /// <summary>
    /// Returns the unique name assigned by the message bus.
    /// </summary>
    public Task<string?> GetUniqueNameAsync()
    {
        return Task.FromResult(_tmdsConnection.UniqueName);
    }

    /// <summary>
    /// Disposes the connection.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _matchDisposable?.Dispose();
        _matchDisposable = null;

        // Fail any remaining pending method calls
        foreach (var kvp in _pendingMethodCalls)
        {
            if (_pendingMethodCalls.TryRemove(kvp.Key, out var ctx))
            {
                try
                {
                    ctx.ReplyError("org.freedesktop.DBus.Error.Failed", "Connection disposed");
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                finally
                {
                    ctx.Dispose();
                }
            }
        }

        _receiving.Writer.TryComplete();
        _tmdsConnection.Dispose();
        await Task.CompletedTask;
    }

    // --- Factory methods ---

    /// <summary>
    /// Connects to a D-Bus address.
    /// </summary>
    public static Task<TmdsDBusWireConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        if (string.Equals(address, "session", StringComparison.OrdinalIgnoreCase))
            return ConnectSessionAsync(cancellationToken);

        if (string.Equals(address, "system", StringComparison.OrdinalIgnoreCase))
            return ConnectSystemAsync(cancellationToken);

        return ConnectCoreAsync(address, cancellationToken);
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static Task<TmdsDBusWireConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        var address = DBusAddress.Session;
        if (string.IsNullOrEmpty(address))
            throw new InvalidOperationException("DBUS_SESSION_BUS_ADDRESS is not set.");
        return ConnectCoreAsync(address, cancellationToken);
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<TmdsDBusWireConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        var address = DBusAddress.System;
        if (string.IsNullOrEmpty(address))
            address = "unix:path=/var/run/dbus/system_bus_socket";
        return ConnectCoreAsync(address, cancellationToken);
    }

    private static async Task<TmdsDBusWireConnection> ConnectCoreAsync(
        string address,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tmdsConn = new global::Tmds.DBus.Protocol.DBusConnection(address);
        await tmdsConn.ConnectAsync().ConfigureAwait(false);

        var wire = new TmdsDBusWireConnection(tmdsConn);

        // Register ourselves as the root method handler for all incoming method calls
        tmdsConn.AddMethodHandler(wire);

        // Catch-all AddMatch for signals — empty MatchRule matches everything
        wire._matchDisposable = await tmdsConn.AddMatchAsync<DBusMessage>(
            new MatchRule { Type = MessageType.Signal },
            static (Message message, object? _) => TmdsTypeConverter.ToDBusMessage(message),
            (Exception? ex, DBusMessage msg, object? rs, object? hs) =>
            {
                if (ex is not null)
                    return;

                ((TmdsDBusWireConnection)hs!)._receiving.Writer.TryWrite(msg);
            },
            ObserverFlags.None,
            readerState: null,
            handlerState: wire,
            emitOnCapturedContext: false).ConfigureAwait(false);

        return wire;
    }
}
