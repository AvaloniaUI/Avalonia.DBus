using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Managed;
using Avalonia.DBus.Transport;
using Xunit;

namespace Avalonia.DBus.Tests.Transport;

public class UnixSocketDBusTransportTests
{
    private static readonly ManagedDBusMessageSerializer s_serializer = new();

    private static DBusSerializedMessage CreateTestMessage(string body, uint serial = 1)
    {
        var msg = DBusMessage.CreateSignal("/org/test", "org.test.Iface", "Ping", body);
        msg.Serial = serial;
        return s_serializer.Serialize(msg);
    }

    private static async Task<(Socket client, Socket server, string path)> CreateSocketPairAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"avdbus-test-{Guid.NewGuid()}.sock");
        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        listener.Listen(1);

        var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(path));
        var serverSocket = await listener.AcceptAsync();

        listener.Dispose();
        return (clientSocket, serverSocket, path);
    }

    [Fact]
    public async Task FromSocket_RoundTripsMessage()
    {
        var (clientSocket, serverSocket, path) = await CreateSocketPairAsync();
        try
        {
            var (clientReader, clientWriter, clientCts, _, _) = DBusTransport.FromSocket(clientSocket);
            var (serverReader, serverWriter, serverCts, _, _) = DBusTransport.FromSocket(serverSocket);

            // Send a message from client to server
            var original = CreateTestMessage("hello-roundtrip", serial: 42);
            await clientWriter.WriteAsync(original);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await serverReader.ReadAsync(cts.Token);

            // Verify the raw bytes match
            Assert.Equal(original.Message, received.Message);

            // Also verify content via deserialization
            var deserialized = s_serializer.Deserialize(received);
            Assert.Equal(DBusMessageType.Signal, deserialized.Type);
            Assert.Equal("Ping", deserialized.Member);
            Assert.Equal("hello-roundtrip", deserialized.Body[0]);

            // Cleanup: complete writers so reader tasks exit
            clientCts.Cancel();
            serverCts.Cancel();
        }
        finally
        {
            clientSocket.Dispose();
            serverSocket.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FromSocket_HandlesMultipleMessages()
    {
        var (clientSocket, serverSocket, path) = await CreateSocketPairAsync();
        try
        {
            var (clientReader, clientWriter, clientCts, _, _) = DBusTransport.FromSocket(clientSocket);
            var (serverReader, serverWriter, serverCts, _, _) = DBusTransport.FromSocket(serverSocket);

            const int messageCount = 10;

            // Send 10 messages from client to server
            for (int i = 0; i < messageCount; i++)
            {
                var msg = CreateTestMessage($"msg-{i}", serial: (uint)(i + 1));
                await clientWriter.WriteAsync(msg);
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            // Verify all 10 arrive in order
            for (int i = 0; i < messageCount; i++)
            {
                var received = await serverReader.ReadAsync(cts.Token);
                var deserialized = s_serializer.Deserialize(received);
                Assert.Equal($"msg-{i}", deserialized.Body[0]);
            }

            clientCts.Cancel();
            serverCts.Cancel();
        }
        finally
        {
            clientSocket.Dispose();
            serverSocket.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FromSocket_BidirectionalCommunication()
    {
        var (clientSocket, serverSocket, path) = await CreateSocketPairAsync();
        try
        {
            var (clientReader, clientWriter, clientCts, _, _) = DBusTransport.FromSocket(clientSocket);
            var (serverReader, serverWriter, serverCts, _, _) = DBusTransport.FromSocket(serverSocket);

            // Send from client to server
            var clientMsg = CreateTestMessage("from-client", serial: 1);
            await clientWriter.WriteAsync(clientMsg);

            // Send from server to client
            var serverMsg = CreateTestMessage("from-server", serial: 2);
            await serverWriter.WriteAsync(serverMsg);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            // Verify server receives client message
            var receivedByServer = await serverReader.ReadAsync(cts.Token);
            var deserializedByServer = s_serializer.Deserialize(receivedByServer);
            Assert.Equal("from-client", deserializedByServer.Body[0]);

            // Verify client receives server message
            var receivedByClient = await clientReader.ReadAsync(cts.Token);
            var deserializedByClient = s_serializer.Deserialize(receivedByClient);
            Assert.Equal("from-server", deserializedByClient.Body[0]);

            clientCts.Cancel();
            serverCts.Cancel();
        }
        finally
        {
            clientSocket.Dispose();
            serverSocket.Dispose();
            File.Delete(path);
        }
    }
}
