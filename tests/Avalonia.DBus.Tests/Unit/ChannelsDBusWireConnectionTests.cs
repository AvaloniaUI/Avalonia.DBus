using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Managed;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

public class ChannelsDBusWireConnectionTests
{
    private static readonly ManagedDBusMessageSerializer s_serializer = new();

    private static (ChannelsDBusWireConnection Connection,
        Channel<DBusSerializedMessage> Inbound,
        Channel<DBusSerializedMessage> Outbound)
        CreateConnection(
            string? uniqueName = null,
            bool isPeerToPeer = true,
            IDBusDiagnostics? diagnostics = null)
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>();

        var connection = diagnostics == null
            ? new ChannelsDBusWireConnection(
                inbound.Reader,
                outbound.Writer,
                uniqueName: uniqueName,
                isPeerToPeer: isPeerToPeer)
            : new ChannelsDBusWireConnection(
                inbound.Reader,
                outbound.Writer,
                socket: null,
                cts: null,
                uniqueName: uniqueName,
                isPeerToPeer: isPeerToPeer,
                diagnostics: diagnostics);

        return (connection, inbound, outbound);
    }

    [Fact]
    public async Task SendAsync_SerializesAndWritesToChannel()
    {
        var (conn, _, outbound) = CreateConnection();
        await using (conn)
        {
            var signal = DBusMessage.CreateSignal("/org/test", "org.test.Iface", "Ping", "hello");

            await conn.SendAsync(signal);

            var written = await outbound.Reader.ReadAsync();
            Assert.NotNull(written);
            Assert.NotEmpty(written.Message);
        }
    }

    [Fact]
    public async Task SendAsync_AssignsSerial()
    {
        var (conn, _, outbound) = CreateConnection();
        await using (conn)
        {
            var msg = DBusMessage.CreateSignal("/org/test", "org.test.Iface", "Ping");
            Assert.Equal(0u, msg.Serial);

            await conn.SendAsync(msg);

            Assert.NotEqual(0u, msg.Serial);
        }
    }

    [Fact]
    public async Task SendWithReplyAsync_CorrelatesReply()
    {
        var (conn, inbound, outbound) = CreateConnection(uniqueName: ":1.1");
        await using (conn)
        {
            // Send a method call
            var call = DBusMessage.CreateMethodCall(":1.2", "/org/test", "org.test.Iface", "Echo", "world");

            var replyTask = conn.SendWithReplyAsync(call);

            // Read the outgoing call from the outbound channel
            var outgoing = await outbound.Reader.ReadAsync();
            Assert.NotEmpty(outgoing.Message);

            // Deserialize to get the serial
            var deserialized = s_serializer.Deserialize(outgoing);
            Assert.Equal(call.Serial, deserialized.Serial);

            // Build a reply and inject it through the inbound channel
            var reply = new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                ReplySerial = call.Serial,
                Destination = ":1.1",
                Body = ["echoed"]
            };

            var serializedReply = s_serializer.Serialize(reply);
            await inbound.Writer.WriteAsync(serializedReply);

            // Wait for the reply
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var result = await replyTask.WaitAsync(cts.Token);

            Assert.Equal(DBusMessageType.MethodReturn, result.Type);
            Assert.Equal(call.Serial, result.ReplySerial);
            Assert.Equal("echoed", result.Body[0]);
        }
    }

    [Fact]
    public async Task ReceivingReader_DeliversNonReplyMessages()
    {
        var (conn, inbound, _) = CreateConnection();
        await using (conn)
        {
            // Inject a signal through the inbound channel
            var signal = DBusMessage.CreateSignal("/org/test", "org.test.Iface", "Ping", "hello");
            signal.Serial = 42;
            var serialized = s_serializer.Serialize(signal);
            await inbound.Writer.WriteAsync(serialized);

            // Read from the ReceivingReader
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await conn.ReceivingReader.ReadAsync(cts.Token);

            Assert.Equal(DBusMessageType.Signal, received.Type);
            Assert.Equal("Ping", received.Member);
            Assert.Equal("hello", received.Body[0]);
        }
    }

    [Fact]
    public async Task GetUniqueNameAsync_ReturnsCtorParam()
    {
        var (conn, _, _) = CreateConnection(uniqueName: ":1.42");
        await using (conn)
        {
            var name = await conn.GetUniqueNameAsync();
            Assert.Equal(":1.42", name);
        }
    }

    [Fact]
    public async Task IsPeerToPeer_DefaultsTrue()
    {
        var (conn, _, _) = CreateConnection();
        await using (conn)
        {
            Assert.True(conn.IsPeerToPeer);
        }
    }

    [Fact]
    public async Task IsPeerToPeer_CanBeSetFalse()
    {
        var (conn, _, _) = CreateConnection(isPeerToPeer: false);
        await using (conn)
        {
            Assert.False(conn.IsPeerToPeer);
        }
    }

    [Fact]
    public async Task DisposeAsync_FailsPendingReplies()
    {
        var (conn, _, outbound) = CreateConnection();

        var call = DBusMessage.CreateMethodCall(":1.2", "/org/test", "org.test.Iface", "Echo");
        var replyTask = conn.SendWithReplyAsync(call);

        // Consume the outgoing message
        await outbound.Reader.ReadAsync();

        // Dispose the connection
        await conn.DisposeAsync();

        // The pending reply should be faulted with ObjectDisposedException
        var ex = await Assert.ThrowsAsync<ObjectDisposedException>(() => replyTask);
        Assert.Contains("ChannelsDBusWireConnection", ex.ObjectName);
    }

    [Fact]
    public async Task MultipleConcurrentSendWithReplyAsync_CorrelateCorrectly()
    {
        var (conn, inbound, outbound) = CreateConnection(uniqueName: ":1.1");
        await using (conn)
        {
            // Send three method calls concurrently
            var call1 = DBusMessage.CreateMethodCall(":1.2", "/org/test", "org.test.Iface", "M1");
            var call2 = DBusMessage.CreateMethodCall(":1.2", "/org/test", "org.test.Iface", "M2");
            var call3 = DBusMessage.CreateMethodCall(":1.2", "/org/test", "org.test.Iface", "M3");

            var replyTask1 = conn.SendWithReplyAsync(call1);
            var replyTask2 = conn.SendWithReplyAsync(call2);
            var replyTask3 = conn.SendWithReplyAsync(call3);

            // Consume all outgoing messages
            await outbound.Reader.ReadAsync();
            await outbound.Reader.ReadAsync();
            await outbound.Reader.ReadAsync();

            // Reply out of order: 3, 1, 2
            var reply3 = new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                ReplySerial = call3.Serial,
                Destination = ":1.1",
                Body = ["r3"]
            };
            await inbound.Writer.WriteAsync(s_serializer.Serialize(reply3));

            var reply1 = new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                ReplySerial = call1.Serial,
                Destination = ":1.1",
                Body = ["r1"]
            };
            await inbound.Writer.WriteAsync(s_serializer.Serialize(reply1));

            var reply2 = new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                ReplySerial = call2.Serial,
                Destination = ":1.1",
                Body = ["r2"]
            };
            await inbound.Writer.WriteAsync(s_serializer.Serialize(reply2));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var result1 = await replyTask1.WaitAsync(cts.Token);
            var result2 = await replyTask2.WaitAsync(cts.Token);
            var result3 = await replyTask3.WaitAsync(cts.Token);

            Assert.Equal("r1", result1.Body[0]);
            Assert.Equal("r2", result2.Body[0]);
            Assert.Equal("r3", result3.Body[0]);
        }
    }

    [Fact]
    public async Task DisposeAsync_ClosesSocket()
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        var conn = new ChannelsDBusWireConnection(
            inbound.Reader, outbound.Writer,
            socket: socket);

        await conn.DisposeAsync();

        // After disposal, operations on the socket should throw ObjectDisposedException
        Assert.Throws<ObjectDisposedException>(() => socket.Send(Array.Empty<byte>()));
    }

    [Fact]
    public async Task DisposeAsync_CancelsCts()
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        var cts = new CancellationTokenSource();
        var token = cts.Token; // capture before dispose

        var conn = new ChannelsDBusWireConnection(
            inbound.Reader, outbound.Writer,
            cts: cts);

        await conn.DisposeAsync();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task ReceiveLoop_MalformedMessage_SkipsAndContinues()
    {
        var diagnostics = new CollectingDiagnostics();
        var (conn, inbound, _) = CreateConnection(diagnostics: diagnostics);
        await using (conn)
        {
            // Write a malformed message (garbage bytes)
            var malformed = new DBusSerializedMessage(new byte[] { 0xFF, 0x00, 0x01 }, []);
            await inbound.Writer.WriteAsync(malformed);

            // Write a valid signal after the malformed one
            var signal = DBusMessage.CreateSignal("/org/test", "org.test.Iface", "Ping", "hello");
            signal.Serial = 99;
            var valid = s_serializer.Serialize(signal);
            await inbound.Writer.WriteAsync(valid);

            // The valid signal should be delivered despite the earlier malformed message
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await conn.ReceivingReader.ReadAsync(cts.Token);

            Assert.Equal(DBusMessageType.Signal, received.Type);
            Assert.Equal("Ping", received.Member);
            Assert.Contains(
                diagnostics.Logs,
                log => log.Level == DBusLogLevel.Warning
                    && log.Message.Contains("Skipping malformed D-Bus message", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task SendWithReplyAsync_SerializationFailure_DoesNotLeavePendingReply()
    {
        var (conn, inbound, _) = CreateConnection(uniqueName: ":1.1");
        await using (conn)
        {
            var invalidCall = new DBusMessage
            {
                Type = DBusMessageType.MethodCall,
                Destination = ":1.2",
                Path = "/test",
                Interface = "org.test.I",
                Member = "Broken"
            };
            invalidCall.SetBodyWithSignature([new object()], "v");

            await Assert.ThrowsAsync<NotSupportedException>(() => conn.SendWithReplyAsync(invalidCall));
            Assert.Equal(1u, invalidCall.Serial);

            var reply = new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                ReplySerial = invalidCall.Serial,
                Destination = ":1.1",
                Body = ["reply"]
            };

            await inbound.Writer.WriteAsync(s_serializer.Serialize(reply));

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var received = await conn.ReceivingReader.ReadAsync(cts.Token);

            Assert.Equal(DBusMessageType.MethodReturn, received.Type);
            Assert.Equal(invalidCall.Serial, received.ReplySerial);
            Assert.Equal("reply", received.Body[0]);
        }
    }

    [Fact]
    public async Task DisposeAsync_DoubleDispose_NoThrow()
    {
        var (conn, _, _) = CreateConnection();

        await conn.DisposeAsync();
        await conn.DisposeAsync(); // should not throw
    }

    [Fact]
    public async Task SendWithReplyAsync_ChannelCompleted_Throws()
    {
        var (conn, _, outbound) = CreateConnection();
        await using (conn)
        {
            // Complete the outbound channel before sending
            outbound.Writer.TryComplete();

            var call = DBusMessage.CreateMethodCall(":1.2", "/test", "org.test.I", "M");

            // Should throw, not hang forever
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<Exception>(
                () => conn.SendWithReplyAsync(call, cts.Token));
        }
    }

    [Fact]
    public async Task SendWithReplyAsync_Cancelled_ThrowsAndCleansUp()
    {
        var (conn, _, outbound) = CreateConnection();
        await using (conn)
        {
            using var cts = new CancellationTokenSource();

            var call = DBusMessage.CreateMethodCall(":1.2", "/test", "org.test.I", "M");
            var replyTask = conn.SendWithReplyAsync(call, cts.Token);

            // Consume the outgoing message
            await outbound.Reader.ReadAsync();

            // Cancel before reply arrives
            cts.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() => replyTask);
        }
    }

    [Fact]
    public async Task DisposeAsync_ConcurrentSend_DoesNotHang()
    {
        var (conn, _, outbound) = CreateConnection();

        // Start several sends with replies
        var calls = new Task<DBusMessage>[10];
        for (int i = 0; i < calls.Length; i++)
        {
            var call = DBusMessage.CreateMethodCall(":1.2", "/test", "org.test.I", $"M{i}");
            calls[i] = conn.SendWithReplyAsync(call);
        }

        // Drain outbound
        for (int i = 0; i < calls.Length; i++)
            await outbound.Reader.ReadAsync();

        // Dispose while replies are pending
        await conn.DisposeAsync();

        // All pending replies should be faulted (not hanging)
        foreach (var t in calls)
        {
            await Assert.ThrowsAsync<ObjectDisposedException>(() => t);
        }
    }

    private sealed class CollectingDiagnostics : IDBusDiagnostics
    {
        public System.Collections.Generic.List<(DBusLogLevel Level, string Message)> Logs { get; } = [];
        public System.Collections.Generic.List<Exception> UnobservedExceptions { get; } = [];

        public void Log(DBusLogLevel level, string message) => Logs.Add((level, message));

        public void OnUnobservedException(Exception exception) => UnobservedExceptions.Add(exception);
    }
}
