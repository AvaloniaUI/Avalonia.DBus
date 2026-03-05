using System;
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
        CreateConnection(string? uniqueName = null, bool isPeerToPeer = true)
    {
        var inbound = Channel.CreateUnbounded<DBusSerializedMessage>();
        var outbound = Channel.CreateUnbounded<DBusSerializedMessage>();

        var connection = new ChannelsDBusWireConnection(
            inbound.Reader,
            outbound.Writer,
            uniqueName,
            isPeerToPeer);

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
}
