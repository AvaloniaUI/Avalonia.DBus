using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus;

namespace Avalonia.DBus.Transport;

/// <summary>
/// Provides factory methods for bridging connected stream sockets to D-Bus wire framing.
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
    /// This helper intentionally transports only the inline D-Bus message bytes for now.
    /// Managed <see cref="Socket"/> APIs do not currently expose sendmsg/recvmsg-style
    /// SCM_RIGHTS support, so Unix FD passing is logged but not transferred on this path.
    /// </returns>
    public static (ChannelReader<DBusSerializedMessage> Reader,
                   ChannelWriter<DBusSerializedMessage> Writer,
                   CancellationTokenSource Cts,
                   Task ReaderTask,
                   Task WriterTask)
        FromSocket(Socket socket)
        => FromSocket(socket, diagnostics: null);

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
    /// <see cref="ChannelsDBusWireConnection"/> using raw D-Bus wire framing over it.
    /// </summary>
    /// <param name="socketPath">The file-system path of the Unix domain socket.</param>
    /// <returns>A <see cref="ChannelsDBusWireConnection"/> connected to the socket.</returns>
    public static ChannelsDBusWireConnection ConnectUnix(string socketPath)
        => ConnectUnix(socketPath, diagnostics: null);

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
