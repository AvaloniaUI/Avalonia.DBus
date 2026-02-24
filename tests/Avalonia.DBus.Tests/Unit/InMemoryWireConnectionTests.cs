using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Unit;

// --- Test service interfaces and implementations ---

internal interface ITestEchoService
{
    string Echo(string input);
}

internal class TestEchoService : ITestEchoService
{
    public string Echo(string input) => input;
}

internal class TestEchoDispatcher : IDBusInterfaceCallDispatcher
{
    public Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message)
    {
        var input = target is ITestEchoService service && message.Body.Count > 0 && message.Body[0] is string s
            ? service.Echo(s)
            : string.Empty;

        return Task.FromResult(message.CreateReply(input));
    }
}

internal interface ITestFailingService
{
    void Fail();
}

internal class TestFailingService : ITestFailingService
{
    public void Fail() => throw new InvalidOperationException("boom");
}

internal class TestFailingDispatcher : IDBusInterfaceCallDispatcher
{
    public Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message)
    {
        return Task.FromResult(message.CreateError("org.test.Error.Boom", "Something went wrong"));
    }
}

internal interface ITestComplexEchoService
{
}

internal class TestComplexEchoService : ITestComplexEchoService
{
}

internal class ComplexEchoDispatcher : IDBusInterfaceCallDispatcher
{
    public Task<DBusMessage> Handle(IDBusConnection connection, object? target, DBusMessage message)
    {
        // Echo the entire body back — re-triggers signature inference on the reply
        return Task.FromResult(new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            ReplySerial = message.Serial,
            Destination = message.Sender,
            Body = message.Body
        });
    }
}

// --- Registration ---

file static class TestServiceRegistration
{
    static TestServiceRegistration()
    {
        DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
        {
            ClrType = typeof(ITestEchoService),
            InterfaceName = "org.test.Echo",
            CreateHandler = () => new TestEchoDispatcher()
        });

        DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
        {
            ClrType = typeof(ITestFailingService),
            InterfaceName = "org.test.Failing",
            CreateHandler = () => new TestFailingDispatcher()
        });

        DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
        {
            ClrType = typeof(ITestComplexEchoService),
            InterfaceName = "org.test.ComplexEcho",
            CreateHandler = () => new ComplexEchoDispatcher()
        });
    }

    public static void EnsureRegistered()
    {
        // Calling this triggers the static constructor once.
    }
}

// --- Tests ---

/// <summary>
/// All tests use a <see cref="JsonDBusMessageSerializer"/> on the in-memory wire,
/// so every message is serialized → deserialized on transit. This makes every test
/// an implicit marshaller regression test.
/// </summary>
public class InMemoryWireConnectionTests : IAsyncLifetime
{
    private static readonly JsonDBusMessageSerializer s_serializer = new();

    private InMemoryWireConnection _wireA = null!;
    private InMemoryWireConnection _wireB = null!;
    private DBusConnection _connA = null!;
    private DBusConnection _connB = null!;

    public Task InitializeAsync()
    {
        TestServiceRegistration.EnsureRegistered();

        (_wireA, _wireB) = InMemoryWireConnection.CreatePair(":mem.A", ":mem.B", s_serializer);
        _connA = new DBusConnection(_wireA);
        _connB = new DBusConnection(_wireB);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _connA.DisposeAsync();
        await _connB.DisposeAsync();
    }

    [Fact]
    public async Task Signal_SentByA_ReceivedByB()
    {
        var received = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _connB.SubscribeAsync(
            sender: null,
            path: "/org/test",
            iface: "org.test.Signals",
            member: "Ping",
            handler: msg =>
            {
                received.TrySetResult(msg);
                return Task.CompletedTask;
            });

        var signal = DBusMessage.CreateSignal("/org/test", "org.test.Signals", "Ping", "hello");
        await _connA.SendMessageAsync(signal);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        cts.Token.Register(() => received.TrySetCanceled());
        var result = await received.Task;

        Assert.Equal(DBusMessageType.Signal, result.Type);
        Assert.Equal("Ping", result.Member);
        Assert.Equal("hello", result.Body[0]);
    }

    [Fact]
    public async Task Signals_FilteredByInterfaceAndMember()
    {
        var receivedPing = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedPong = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var receivedOther = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        await _connB.SubscribeAsync(
            sender: null, path: "/org/test", iface: "org.test.Signals", member: "Ping",
            handler: msg => { receivedPing.TrySetResult(msg); return Task.CompletedTask; });

        await _connB.SubscribeAsync(
            sender: null, path: "/org/test", iface: "org.test.Signals", member: "Pong",
            handler: msg => { receivedPong.TrySetResult(msg); return Task.CompletedTask; });

        await _connB.SubscribeAsync(
            sender: null, path: "/org/test", iface: "org.test.Other", member: "Nope",
            handler: msg => { receivedOther.TrySetResult(msg); return Task.CompletedTask; });

        // Send Ping and Pong on org.test.Signals
        await _connA.SendMessageAsync(DBusMessage.CreateSignal("/org/test", "org.test.Signals", "Ping", "p1"));
        await _connA.SendMessageAsync(DBusMessage.CreateSignal("/org/test", "org.test.Signals", "Pong", "p2"));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        cts.Token.Register(() =>
        {
            receivedPing.TrySetCanceled();
            receivedPong.TrySetCanceled();
            receivedOther.TrySetCanceled();
        });

        var ping = await receivedPing.Task;
        var pong = await receivedPong.Task;

        Assert.Equal("Ping", ping.Member);
        Assert.Equal("p1", ping.Body[0]);
        Assert.Equal("Pong", pong.Member);
        Assert.Equal("p2", pong.Body[0]);

        // The "Other" interface subscription should NOT have received anything
        Assert.False(receivedOther.Task.IsCompleted);
    }

    [Fact]
    public async Task MethodCall_WithRegisteredHandler_ReturnsReply()
    {
        var echoService = new TestEchoService();
        using var reg = await _connB.RegisterObjects(
            "/org/test/echo",
            [echoService]);

        var reply = await _connA.CallMethodAsync(
            ":mem.B",
            "/org/test/echo",
            "org.test.Echo",
            "Echo",
            cancellationToken: default,
            "world");

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Equal("world", reply.Body[0]);
    }

    [Fact]
    public async Task MethodCall_ToUnregisteredPath_ReturnsUnknownObjectError()
    {
        var ex = await Assert.ThrowsAsync<DBusException>(() =>
            _connA.CallMethodAsync(
                ":mem.B",
                "/org/does/not/exist",
                "org.test.Echo",
                "Echo"));

        Assert.Equal("org.freedesktop.DBus.Error.UnknownObject", ex.ErrorName);
    }

    [Fact]
    public async Task MethodCall_HandlerReturnsError_CallerGetsDBusException()
    {
        var failService = new TestFailingService();
        using var reg = await _connB.RegisterObjects(
            "/org/test/fail",
            [failService]);

        var ex = await Assert.ThrowsAsync<DBusException>(() =>
            _connA.CallMethodAsync(
                ":mem.B",
                "/org/test/fail",
                "org.test.Failing",
                "Fail"));

        Assert.Equal("org.test.Error.Boom", ex.ErrorName);
        Assert.Contains("Something went wrong", ex.Message);
    }

    [Fact]
    public async Task GetUniqueName_ReturnsConfiguredName()
    {
        var nameA = await _connA.GetUniqueNameAsync();
        var nameB = await _connB.GetUniqueNameAsync();

        Assert.Equal(":mem.A", nameA);
        Assert.Equal(":mem.B", nameB);
    }

    [Fact]
    public async Task Dispose_CompletesWithoutHanging()
    {
        var (wireA, _) = InMemoryWireConnection.CreatePair(":dispose.A", ":dispose.B", s_serializer);
        var conn = new DBusConnection(wireA);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var disposeTask = conn.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(5), cts.Token));

        Assert.Same(disposeTask, completed);
    }

    [Fact]
    public async Task MethodCall_ComplexSignature_RoundTripsAllTypes()
    {
        // Build a body that contains every D-Bus type
        object[] body =
        [
            // --- Basic types ---
            (byte)0xFF,                                          // y
            true,                                                // b
            (short)-1234,                                        // n
            (ushort)1234,                                        // q
            42,                                                  // i
            (uint)42,                                            // u
            (long)-99999,                                        // x
            (ulong)99999,                                        // t
            3.14,                                                // d
            "hello",                                             // s

            // --- Special basic types ---
            new DBusObjectPath("/org/example"),                  // o
            new DBusSignature("a{sv}"),                          // g
            new DBusUnixFd(7),                                   // h

            // --- Variant ---
            new DBusVariant("wrapped"),                          // v

            // --- Array ---
            new List<int> { 1, 2, 3 },                           // ai

            // --- Dictionary ---
            new Dictionary<string, DBusVariant>                  // a{sv}
            {
                ["key1"] = new(42),
                ["key2"] = new("val"),
            },

            // --- Struct ---
            new DBusStruct("alice", 30, 1.75),                   // (sid)

            // --- Array of structs ---
            new List<DBusStruct>                                 // a(si)
            {
                new("bob", 25),
                new("carol", 35),
            },

            // --- Dict with array values ---
            new Dictionary<string, List<int>>                    // a{sai}
            {
                ["primes"] = [2, 3, 5],
                ["evens"] = [2, 4, 6],
            },

            // --- Variant wrapping a struct ---
            new DBusVariant
                (new DBusStruct("nested", 99)),                  // v

            // --- Nested array ---
            new List<List<int>>                                  // aai
            {
                new() { 10, 20 },
                new() { 30, 40 },
            },
        ];

        const string expectedSignature = "ybnqiuxtdsoghvaia{sv}(sid)a(si)a{sai}vaai";

        var complexService = new TestComplexEchoService();
        using var reg = await _connB.RegisterObjects(
            "/org/test/complex",
            [complexService]);

        // Send complex body through method call round-trip (marshalled twice: call + reply)
        var call = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Destination = ":mem.B",
            Path = "/org/test/complex",
            Interface = "org.test.ComplexEcho",
            Member = "Echo",
            Body = body,
        };

        Assert.Equal(expectedSignature, call.Signature.Value);

        var completion = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Use the low-level send path so we get the raw reply
        // We'll subscribe to the control channel through CallMethodAsync
        var reply = await _connA.CallMethodAsync(
            ":mem.B",
            "/org/test/complex",
            "org.test.ComplexEcho",
            "Echo",
            cancellationToken: default,
            body);

        // --- Validate signature survived the round-trip ---
        Assert.Equal(expectedSignature, reply.Signature.Value);
        Assert.Equal(body.Length, reply.Body.Count);

        var b = reply.Body;
        var idx = 0;

        // Basic types — verify CLR type and value
        Assert.IsType<byte>(b[idx]);
        Assert.Equal((byte)0xFF, b[idx++]);

        Assert.IsType<bool>(b[idx]);
        Assert.Equal(true, b[idx++]);

        Assert.IsType<short>(b[idx]);
        Assert.Equal((short)-1234, b[idx++]);

        Assert.IsType<ushort>(b[idx]);
        Assert.Equal((ushort)1234, b[idx++]);

        Assert.IsType<int>(b[idx]);
        Assert.Equal(42, b[idx++]);

        Assert.IsType<uint>(b[idx]);
        Assert.Equal(42u, b[idx++]);

        Assert.IsType<long>(b[idx]);
        Assert.Equal(-99999L, b[idx++]);

        Assert.IsType<ulong>(b[idx]);
        Assert.Equal(99999UL, b[idx++]);

        Assert.IsType<double>(b[idx]);
        Assert.Equal(3.14, b[idx++]);

        Assert.IsType<string>(b[idx]);
        Assert.Equal("hello", b[idx++]);

        // Special basic types
        Assert.IsType<DBusObjectPath>(b[idx]);
        Assert.Equal(new DBusObjectPath("/org/example"), b[idx++]);

        Assert.IsType<DBusSignature>(b[idx]);
        Assert.Equal(new DBusSignature("a{sv}"), b[idx++]);

        Assert.IsType<DBusUnixFd>(b[idx]);
        Assert.Equal(new DBusUnixFd(7), b[idx++]);

        // Variant wrapping string
        var variant0 = Assert.IsType<DBusVariant>(b[idx++]);
        Assert.Equal("s", variant0.Signature.Value);
        Assert.Equal("wrapped", variant0.Value);

        // List<int>
        var intList = Assert.IsType<List<int>>(b[idx++]);
        Assert.Equal([1, 2, 3], intList);

        // Dictionary<string, DBusVariant>
        var svDict = Assert.IsType<Dictionary<string, DBusVariant>>(b[idx++]);
        Assert.Equal(2, svDict.Count);
        Assert.Equal(42, svDict["key1"].Value);
        Assert.Equal("val", svDict["key2"].Value);

        // Struct (sid)
        var s0 = Assert.IsType<DBusStruct>(b[idx++]);
        Assert.Equal(3, s0.Count);
        Assert.Equal("alice", s0[0]);
        Assert.Equal(30, s0[1]);
        Assert.Equal(1.75, s0[2]);

        // Array of structs a(si)
        var structList = Assert.IsType<List<DBusStruct>>(b[idx++]);
        Assert.Equal(2, structList.Count);
        Assert.Equal("bob", structList[0][0]);
        Assert.Equal(25, structList[0][1]);
        Assert.Equal("carol", structList[1][0]);
        Assert.Equal(35, structList[1][1]);

        // Dict with array values a{sai}
        var saiDict = Assert.IsType<Dictionary<string, List<int>>>(b[idx++]);
        Assert.Equal([2, 3, 5], saiDict["primes"]);
        Assert.Equal([2, 4, 6], saiDict["evens"]);

        // Variant wrapping struct
        var variant1 = Assert.IsType<DBusVariant>(b[idx++]);
        Assert.Equal("(si)", variant1.Signature.Value);
        var innerStruct = Assert.IsType<DBusStruct>(variant1.Value);
        Assert.Equal("nested", innerStruct[0]);
        Assert.Equal(99, innerStruct[1]);

        // Nested array aai
        var nestedList = Assert.IsType<List<List<int>>>(b[idx++]);
        Assert.Equal(2, nestedList.Count);
        Assert.Equal([10, 20], nestedList[0]);
        Assert.Equal([30, 40], nestedList[1]);

        Assert.Equal(body.Length, idx);
    }
}
