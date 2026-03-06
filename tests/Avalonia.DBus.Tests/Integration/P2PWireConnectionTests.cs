using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Transport;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class P2PWireConnectionTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Creates a Unix socket pair (listener + client + accept) and wires up
    /// ChannelsDBusWireConnection on both sides for peer-to-peer testing.
    /// </summary>
    private static async Task RunWithSocketPairAsync(
        Func<ChannelsDBusWireConnection, ChannelsDBusWireConnection, Task> testAction)
    {
        var path = Path.Combine(Path.GetTempPath(), $"avdbus-e2e-{Guid.NewGuid()}.sock");
        try
        {
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(path));
            listener.Listen(1);

            using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await clientSocket.ConnectAsync(new UnixDomainSocketEndPoint(path));
            using var serverSocket = await listener.AcceptAsync();

            var (clientReader, clientWriter, clientCts, _, _) = DBusTransport.FromSocket(clientSocket);
            var (serverReader, serverWriter, serverCts, _, _) = DBusTransport.FromSocket(serverSocket);

            await using var client = new ChannelsDBusWireConnection(
                clientReader, clientWriter, socket: clientSocket, cts: clientCts, isPeerToPeer: true);
            await using var server = new ChannelsDBusWireConnection(
                serverReader, serverWriter, socket: serverSocket, cts: serverCts, isPeerToPeer: true);

            await testAction(client, server);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best effort */ }
        }
    }

    [Fact]
    public async Task P2P_RequestReply_OverUnixSocket()
    {
        await RunWithSocketPairAsync(async (client, server) =>
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            // Client sends a MethodCall with args (non-blocking via SendWithReplyAsync)
            var call = DBusMessage.CreateMethodCall(
                "org.test.Server",
                "/org/test/Object",
                "org.test.Interface",
                "Echo",
                "hello", 42);

            var replyTask = client.SendWithReplyAsync(call, cts.Token);

            // Server reads the call from ReceivingReader
            var receivedCall = await server.ReceivingReader.ReadAsync(cts.Token);

            // Verify call contents
            Assert.Equal(DBusMessageType.MethodCall, receivedCall.Type);
            Assert.Equal("Echo", receivedCall.Member);
            Assert.Equal("org.test.Interface", receivedCall.Interface);
            Assert.Equal("/org/test/Object", receivedCall.Path!.Value.Value);
            Assert.Equal(2, receivedCall.Body.Count);
            Assert.Equal("hello", receivedCall.Body[0]);
            Assert.Equal(42, receivedCall.Body[1]);

            // Server sends reply
            var reply = receivedCall.CreateReply("world", 99);
            await server.SendAsync(reply, cts.Token);

            // Client receives reply
            var clientReply = await replyTask;

            // Verify reply
            Assert.Equal(DBusMessageType.MethodReturn, clientReply.Type);
            Assert.Equal(call.Serial, clientReply.ReplySerial);
            Assert.Equal(2, clientReply.Body.Count);
            Assert.Equal("world", clientReply.Body[0]);
            Assert.Equal(99, clientReply.Body[1]);
        });
    }

    [Fact]
    public async Task P2P_Signal_OverUnixSocket()
    {
        await RunWithSocketPairAsync(async (client, server) =>
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            // Server sends a Signal
            var signal = DBusMessage.CreateSignal(
                "/org/test/Object",
                "org.test.Interface",
                "SomethingHappened",
                "event-data", (long)12345);

            await server.SendAsync(signal, cts.Token);

            // Client reads signal from ReceivingReader
            var received = await client.ReceivingReader.ReadAsync(cts.Token);

            // Verify signal
            Assert.Equal(DBusMessageType.Signal, received.Type);
            Assert.Equal("SomethingHappened", received.Member);
            Assert.Equal("org.test.Interface", received.Interface);
            Assert.Equal("/org/test/Object", received.Path!.Value.Value);
            Assert.Equal(2, received.Body.Count);
            Assert.Equal("event-data", received.Body[0]);
            Assert.Equal((long)12345, received.Body[1]);
        });
    }

    [Fact]
    public async Task P2P_ErrorReply_OverUnixSocket()
    {
        await RunWithSocketPairAsync(async (client, server) =>
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            // Client sends a MethodCall
            var call = DBusMessage.CreateMethodCall(
                "org.test.Server",
                "/org/test/Object",
                "org.test.Interface",
                "FailingMethod",
                "some-arg");

            var replyTask = client.SendWithReplyAsync(call, cts.Token);

            // Server reads the call
            var receivedCall = await server.ReceivingReader.ReadAsync(cts.Token);
            Assert.Equal("FailingMethod", receivedCall.Member);

            // Server sends error reply
            var errorReply = receivedCall.CreateError(
                "org.freedesktop.DBus.Error.Failed",
                "something went wrong");
            await server.SendAsync(errorReply, cts.Token);

            // Client receives error reply
            var clientReply = await replyTask;

            // Verify it's an Error type with correct ErrorName
            Assert.Equal(DBusMessageType.Error, clientReply.Type);
            Assert.Equal(call.Serial, clientReply.ReplySerial);
            Assert.Equal("org.freedesktop.DBus.Error.Failed", clientReply.ErrorName);
            Assert.True(clientReply.IsError("org.freedesktop.DBus.Error.Failed"));
            Assert.Single(clientReply.Body);
            Assert.Equal("something went wrong", clientReply.Body[0]);
        });
    }

    [Fact]
    public async Task P2P_MultipleInterleavedRequests_OverUnixSocket()
    {
        await RunWithSocketPairAsync(async (client, server) =>
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            // Client sends 3 method calls concurrently
            var call1 = DBusMessage.CreateMethodCall(
                "org.test.Server", "/", "org.test.Interface", "Method1", "arg1");
            var call2 = DBusMessage.CreateMethodCall(
                "org.test.Server", "/", "org.test.Interface", "Method2", "arg2");
            var call3 = DBusMessage.CreateMethodCall(
                "org.test.Server", "/", "org.test.Interface", "Method3", "arg3");

            var replyTask1 = client.SendWithReplyAsync(call1, cts.Token);
            var replyTask2 = client.SendWithReplyAsync(call2, cts.Token);
            var replyTask3 = client.SendWithReplyAsync(call3, cts.Token);

            // Server reads all 3 calls
            var received1 = await server.ReceivingReader.ReadAsync(cts.Token);
            var received2 = await server.ReceivingReader.ReadAsync(cts.Token);
            var received3 = await server.ReceivingReader.ReadAsync(cts.Token);

            // Collect them by member name for deterministic mapping
            var receivedByMember = new System.Collections.Generic.Dictionary<string, DBusMessage>
            {
                [received1.Member!] = received1,
                [received2.Member!] = received2,
                [received3.Member!] = received3,
            };

            Assert.Equal(3, receivedByMember.Count);
            Assert.Contains("Method1", receivedByMember.Keys);
            Assert.Contains("Method2", receivedByMember.Keys);
            Assert.Contains("Method3", receivedByMember.Keys);

            // Server replies in reverse order (3, 2, 1)
            var reply3 = receivedByMember["Method3"].CreateReply("reply3");
            var reply2 = receivedByMember["Method2"].CreateReply("reply2");
            var reply1 = receivedByMember["Method1"].CreateReply("reply1");

            await server.SendAsync(reply3, cts.Token);
            await server.SendAsync(reply2, cts.Token);
            await server.SendAsync(reply1, cts.Token);

            // All 3 client tasks complete with correct replies
            var result1 = await replyTask1;
            var result2 = await replyTask2;
            var result3 = await replyTask3;

            Assert.Equal(DBusMessageType.MethodReturn, result1.Type);
            Assert.Equal(call1.Serial, result1.ReplySerial);
            Assert.Single(result1.Body);
            Assert.Equal("reply1", result1.Body[0]);

            Assert.Equal(DBusMessageType.MethodReturn, result2.Type);
            Assert.Equal(call2.Serial, result2.ReplySerial);
            Assert.Single(result2.Body);
            Assert.Equal("reply2", result2.Body[0]);

            Assert.Equal(DBusMessageType.MethodReturn, result3.Type);
            Assert.Equal(call3.Serial, result3.ReplySerial);
            Assert.Single(result3.Body);
            Assert.Equal("reply3", result3.Body[0]);
        });
    }
}
