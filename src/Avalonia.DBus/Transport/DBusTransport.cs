using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;

namespace Avalonia.DBus.Transport;

/// <summary>
/// Provides factory methods for creating D-Bus transport connections from sockets.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
static class DBusTransport
{
    /// <summary>
    /// Bridges a connected <see cref="Socket"/> to a pair of channels carrying
    /// <see cref="DBusSerializedMessage"/> using D-Bus wire framing.
    /// </summary>
    /// <param name="socket">A connected stream socket (e.g., Unix domain socket).</param>
    /// <returns>
    /// A tuple of (reader, writer) where:
    /// <list type="bullet">
    ///   <item><c>reader</c> — read D-Bus messages arriving on the socket</item>
    ///   <item><c>writer</c> — write D-Bus messages to send over the socket</item>
    /// </list>
    /// </returns>
    public static (ChannelReader<DBusSerializedMessage> reader,
                   ChannelWriter<DBusSerializedMessage> writer)
        FromSocket(Socket socket)
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = false });
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        var cts = new CancellationTokenSource();

        // Start the background reader (socket → inbound channel)
        _ = UnixSocketDBusTransport.StartReaderAsync(socket, inbound.Writer, cts.Token);

        // Start the background writer (outbound channel → socket)
        _ = UnixSocketDBusTransport.StartWriterAsync(socket, outbound.Reader, cts.Token);

        return (inbound.Reader, outbound.Writer);
    }

    /// <summary>
    /// Connects to a Unix domain socket at <paramref name="socketPath"/> and returns
    /// a <see cref="ChannelsDBusWireConnection"/> for D-Bus communication.
    /// </summary>
    /// <param name="socketPath">The file-system path of the Unix domain socket.</param>
    /// <returns>A <see cref="ChannelsDBusWireConnection"/> connected to the socket.</returns>
    public static ChannelsDBusWireConnection ConnectUnix(string socketPath)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(socketPath));

        var (reader, writer) = FromSocket(socket);
        return new ChannelsDBusWireConnection(reader, writer);
    }
}
