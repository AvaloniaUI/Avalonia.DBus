using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.DBus;

/// <summary>
/// Abstraction for low-level D-Bus message transport.
/// Implement this interface to provide a custom transport (e.g., named pipes, TCP).
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
interface IDBusWireConnection : IAsyncDisposable
{
    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    Task SendAsync(DBusMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    Task<DBusMessage> SendWithReplyAsync(DBusMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reader for incoming messages (METHOD_CALL, SIGNAL, etc.).
    /// </summary>
    ChannelReader<DBusMessage> ReceivingReader { get; }

    /// <summary>
    /// The unique name assigned by the message bus (e.g., ":1.42").
    /// Null if not connected to a message bus or in peer-to-peer mode.
    /// </summary>
    Task<string?> GetUniqueNameAsync();

    /// <summary>
    /// Returns true if this connection is peer-to-peer (no message bus).
    /// When true, AddMatch/RemoveMatch bus methods are skipped.
    /// </summary>
    bool IsPeerToPeer => false;
}
