using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

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
    /// A 5-tuple of (Reader, Writer, Cts, ReaderTask, WriterTask) where:
    /// <list type="bullet">
    ///   <item><c>Reader</c> — read D-Bus messages arriving on the socket</item>
    ///   <item><c>Writer</c> — write D-Bus messages to send over the socket</item>
    ///   <item><c>Cts</c> — the <see cref="CancellationTokenSource"/> controlling the background tasks</item>
    ///   <item><c>ReaderTask</c> — the background task reading from the socket</item>
    ///   <item><c>WriterTask</c> — the background task writing to the socket</item>
    /// </list>
    /// </returns>
    public static (ChannelReader<DBusSerializedMessage> Reader,
                   ChannelWriter<DBusSerializedMessage> Writer,
                   CancellationTokenSource Cts,
                   Task ReaderTask,
                   Task WriterTask)
        FromSocket(Socket socket)
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = false });
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        var cts = new CancellationTokenSource();

        var readerTask = UnixSocketDBusTransport.StartReaderAsync(socket, inbound.Writer, cts.Token);
        var writerTask = UnixSocketDBusTransport.StartWriterAsync(socket, outbound.Reader, cts.Token);

        return (inbound.Reader, outbound.Writer, cts, readerTask, writerTask);
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

        var (reader, writer, cts, _, _) = FromSocket(socket);
        return new ChannelsDBusWireConnection(reader, writer, socket: socket, cts: cts);
    }
}
