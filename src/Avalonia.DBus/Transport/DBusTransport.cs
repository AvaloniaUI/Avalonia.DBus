using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus;

namespace Avalonia.DBus.Transport;

/// <summary>
/// Provides factory methods for bridging connected stream sockets to raw D-Bus wire framing.
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
    /// <param name="socket">Any already-connected stream socket supplied by the caller.</param>
    /// <returns>
    /// A 5-tuple of (Reader, Writer, Cts, ReaderTask, WriterTask) where:
    /// <list type="bullet">
    ///   <item><c>Reader</c> — read D-Bus messages arriving on the socket</item>
    ///   <item><c>Writer</c> — write D-Bus messages to send over the socket</item>
    ///   <item><c>Cts</c> — the <see cref="CancellationTokenSource"/> controlling the background tasks</item>
    ///   <item><c>ReaderTask</c> — the background task reading from the socket</item>
    ///   <item><c>WriterTask</c> — the background task writing to the socket</item>
    /// </list>
    /// This helper transports only the inline D-Bus message bytes.
    /// Inbound ancillary Unix file descriptors are not reconstructed on the returned reader,
    /// and outbound file descriptors are logged and dropped by the writer path.
    /// </returns>
    public static (ChannelReader<DBusSerializedMessage> Reader,
                   ChannelWriter<DBusSerializedMessage> Writer,
                   CancellationTokenSource Cts,
                   Task ReaderTask,
                   Task WriterTask)
        FromSocket(Socket socket)
        => FromSocket(socket, diagnostics: null);

    /// <summary>
    /// Bridges a connected <see cref="Socket"/> to a pair of channels carrying
    /// <see cref="DBusSerializedMessage"/> using D-Bus wire framing.
    /// </summary>
    /// <param name="socket">Any already-connected stream socket supplied by the caller.</param>
    /// <param name="diagnostics">Receives warnings and unobserved background task failures from the transport.</param>
    /// <returns>
    /// A 5-tuple of (Reader, Writer, Cts, ReaderTask, WriterTask) where:
    /// <list type="bullet">
    ///   <item><c>Reader</c> reads D-Bus messages arriving on the socket.</item>
    ///   <item><c>Writer</c> writes D-Bus messages to send over the socket.</item>
    ///   <item><c>Cts</c> controls the background reader and writer tasks.</item>
    ///   <item><c>ReaderTask</c> is the background task reading from the socket.</item>
    ///   <item><c>WriterTask</c> is the background task writing to the socket.</item>
    /// </list>
    /// This helper transports only the inline D-Bus message bytes.<br/><br/>
    /// For now, inbound ancillary Unix file descriptors are not reconstructed on the returned reader,
    /// and outbound file descriptors are logged and dropped by the writer path.
    /// </returns>
    public static (ChannelReader<DBusSerializedMessage> Reader,
                   ChannelWriter<DBusSerializedMessage> Writer,
                   CancellationTokenSource Cts,
                   Task ReaderTask,
                   Task WriterTask)
        FromSocket(Socket socket, IDBusDiagnostics? diagnostics)
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = true, SingleReader = false });
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>(
            new UnboundedChannelOptions { SingleWriter = false, SingleReader = true });

        var cts = new CancellationTokenSource();

        var readerTask = UnixSocketDBusTransport.StartReaderAsync(socket, inbound.Writer, cts.Token, diagnostics);
        var writerTask = UnixSocketDBusTransport.StartWriterAsync(socket, outbound.Reader, cts.Token, diagnostics);

        ObserveBackgroundFault(readerTask, diagnostics);
        ObserveBackgroundFault(writerTask, diagnostics);

        return (inbound.Reader, outbound.Writer, cts, readerTask, writerTask);
    }

    /// <summary>
    /// Convenience helper that opens a Unix domain stream socket and returns a
    /// peer-to-peer <see cref="ChannelsDBusWireConnection"/> using raw D-Bus wire framing over it.
    /// This helper does not perform D-Bus authentication or the bus <c>Hello</c> exchange.
    /// </summary>
    /// <param name="socketPath">The file-system path of the Unix domain socket.</param>
    /// <returns>A <see cref="ChannelsDBusWireConnection"/> connected to the socket.</returns>
    public static ChannelsDBusWireConnection ConnectUnix(string socketPath)
        => ConnectUnix(socketPath, diagnostics: null);

    /// <summary>
    /// Convenience helper that opens a Unix domain stream socket and returns a
    /// peer-to-peer <see cref="ChannelsDBusWireConnection"/> using raw D-Bus wire framing over it.
    /// This helper does not perform D-Bus authentication or the bus <c>Hello</c> exchange.
    /// </summary>
    /// <param name="socketPath">The file-system path of the Unix domain socket.</param>
    /// <param name="diagnostics">Receives warnings and unobserved background task failures from the transport.</param>
    /// <returns>A <see cref="ChannelsDBusWireConnection"/> connected to the socket.</returns>
    public static ChannelsDBusWireConnection ConnectUnix(string socketPath, IDBusDiagnostics? diagnostics)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        socket.Connect(new UnixDomainSocketEndPoint(socketPath));

        var (reader, writer, cts, _, _) = FromSocket(socket, diagnostics);
        return new ChannelsDBusWireConnection(
            reader,
            writer,
            socket: socket,
            cts: cts,
            uniqueName: null,
            isPeerToPeer: true,
            diagnostics: diagnostics);
    }

    private static void ObserveBackgroundFault(Task task, IDBusDiagnostics? diagnostics)
    {
        if (diagnostics == null)
            return;

        _ = task.ContinueWith(
            static (t, state) =>
            {
                if (state is IDBusDiagnostics sink && t.Exception != null)
                    sink.OnUnobservedException(t.Exception);
            },
            diagnostics,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
