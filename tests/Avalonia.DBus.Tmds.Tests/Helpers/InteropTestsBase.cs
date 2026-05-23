using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Avalonia.DBus.Tmds.Tests.Helpers;

/// <summary>
/// Base class for interop tests between two transport implementations.
/// Subclasses define which transport backs the client and which backs the server.
/// All test methods run in both directions (Tmds→libdbus and libdbus→Tmds).
/// </summary>
public abstract class InteropTestsBase(TmdsInteropFixture fixture)
{
    protected const string EchoInterface = "org.avalonia.dbus.tmds.Echo";
    protected static readonly DBusObjectPath EchoPath = "/org/avalonia/dbus/tmds/Echo";

    protected TmdsInteropFixture Fixture { get; } = fixture;

    protected static string TestName() => $"org.avalonia.dbus.tmds.t{Guid.NewGuid():N}";

    /// <summary>
    /// Creates a connection backed by the "client" transport for this test direction.
    /// </summary>
    protected abstract Task<DBusConnection> CreateClientConnectionAsync();

    /// <summary>
    /// Creates a connection backed by the "server" transport for this test direction.
    /// </summary>
    protected abstract Task<DBusConnection> CreateServerConnectionAsync();

    /// <summary>
    /// Sends a single value through Echo (server echoes back the first arg with its exact signature).
    /// </summary>
    protected async Task<DBusMessage> EchoAsync(object value)
    {
        var name = TestName();
        EchoHandlerHelper.EnsureRegistered();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);
        using var reg = await server.RegisterObjects(EchoPath, [new EchoTarget()]);

        await using var client = await CreateClientConnectionAsync();
        return await client.CallMethodAsync(
            name, EchoPath, EchoInterface, "Echo", CancellationToken.None, value);
    }

    /// <summary>
    /// Sends multiple args through EchoAll (server echoes back all args preserving signatures).
    /// </summary>
    protected async Task<DBusMessage> EchoAllAsync(params object[] args)
    {
        var name = TestName();
        EchoHandlerHelper.EnsureRegistered();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);
        using var reg = await server.RegisterObjects(EchoPath, [new EchoTarget()]);

        await using var client = await CreateClientConnectionAsync();
        return await client.CallMethodAsync(
            name, EchoPath, EchoInterface, "EchoAll", CancellationToken.None, args);
    }

    // ==================== Basic Operations ====================

    [TmdsInteropFact]
    public async Task Echo_StringArg()
    {
        var reply = await EchoAsync("hello interop");
        Assert.NotEmpty(reply.Body);
        Assert.Equal("hello interop", reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Add_IntArgs()
    {
        var name = TestName();
        EchoHandlerHelper.EnsureRegistered();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);
        using var reg = await server.RegisterObjects(EchoPath, [new EchoTarget()]);

        await using var client = await CreateClientConnectionAsync();
        var reply = await client.CallMethodAsync(
            name, EchoPath, EchoInterface, "Add", CancellationToken.None, 17, 25);

        Assert.NotEmpty(reply.Body);
        Assert.Equal(42, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Concat_StringArgs()
    {
        var name = TestName();
        EchoHandlerHelper.EnsureRegistered();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);
        using var reg = await server.RegisterObjects(EchoPath, [new EchoTarget()]);

        await using var client = await CreateClientConnectionAsync();
        var reply = await client.CallMethodAsync(
            name, EchoPath, EchoInterface, "Concat", CancellationToken.None, "foo", "bar");

        Assert.NotEmpty(reply.Body);
        Assert.Equal("foobar", reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Negate_LongArg()
    {
        var name = TestName();
        EchoHandlerHelper.EnsureRegistered();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);
        using var reg = await server.RegisterObjects(EchoPath, [new EchoTarget()]);

        await using var client = await CreateClientConnectionAsync();
        var reply = await client.CallMethodAsync(
            name, EchoPath, EchoInterface, "Negate", CancellationToken.None, 9876543210L);

        Assert.NotEmpty(reply.Body);
        Assert.Equal(-9876543210L, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Signal_IntBody()
    {
        var name = TestName();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);

        await using var client = await CreateClientConnectionAsync();

        var signals = new List<int>();
        var signalReceived = new SemaphoreSlim(0);

        using var sub = await client.SubscribeAsync(
            null, EchoPath, EchoInterface, "Ping",
            msg =>
            {
                if (msg.Body.Count > 0 && msg.Body[0] is int v)
                    signals.Add(v);
                signalReceived.Release();
                return Task.CompletedTask;
            });

        for (var i = 1; i <= 3; i++)
        {
            var signal = DBusMessage.CreateSignal(EchoPath, EchoInterface, "Ping", i);
            await server.SendMessageAsync(signal);
        }

        for (var i = 0; i < 3; i++)
            Assert.True(await signalReceived.WaitAsync(TimeSpan.FromSeconds(5)),
                $"Timed out waiting for signal {i + 1}");

        Assert.Equal([1, 2, 3], signals);
    }

    // ==================== Primitive Type Round-Trips ====================

    [TmdsInteropFact]
    public async Task Byte_RoundTrips()
    {
        var reply = await EchoAsync((byte)255);
        Assert.Equal((byte)255, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Bool_True_RoundTrips()
    {
        var reply = await EchoAsync(true);
        Assert.Equal(true, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Bool_False_RoundTrips()
    {
        var reply = await EchoAsync(false);
        Assert.Equal(false, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Int16_RoundTrips()
    {
        var reply = await EchoAsync((short)-12345);
        Assert.Equal((short)-12345, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task UInt16_RoundTrips()
    {
        var reply = await EchoAsync((ushort)65534);
        Assert.Equal((ushort)65534, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Int32_RoundTrips()
    {
        var reply = await EchoAsync(int.MinValue);
        Assert.Equal(int.MinValue, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task UInt32_RoundTrips()
    {
        var reply = await EchoAsync(uint.MaxValue);
        Assert.Equal(uint.MaxValue, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Int64_RoundTrips()
    {
        var reply = await EchoAsync(long.MinValue);
        Assert.Equal(long.MinValue, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task UInt64_RoundTrips()
    {
        var reply = await EchoAsync(ulong.MaxValue);
        Assert.Equal(ulong.MaxValue, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Double_RoundTrips()
    {
        var reply = await EchoAsync(Math.PI);
        Assert.Equal(Math.PI, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task Double_NegativeInfinity_RoundTrips()
    {
        var reply = await EchoAsync(double.NegativeInfinity);
        Assert.Equal(double.NegativeInfinity, reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task String_Empty_RoundTrips()
    {
        var reply = await EchoAsync("");
        Assert.Equal("", reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task String_Unicode_RoundTrips()
    {
        var reply = await EchoAsync("Привет 🚀");
        Assert.Equal("Привет 🚀", reply.Body[0]);
    }

    [TmdsInteropFact]
    public async Task ObjectPath_RoundTrips()
    {
        var reply = await EchoAsync(new DBusObjectPath("/org/test/path"));
        var result = Assert.IsType<DBusObjectPath>(reply.Body[0]);
        Assert.Equal("/org/test/path", result.Value);
    }

    // ==================== Variant Round-Trips ====================

    [TmdsInteropFact]
    public async Task Variant_String_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant("variant-string"));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal("variant-string", result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Int_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant(42));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(42, result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Bool_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant(true));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(true, result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_InVariant_RoundTrips()
    {
        var inner = new DBusVariant("nested");
        var outer = new DBusVariant(inner);

        var reply = await EchoAsync(outer);
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        var innerResult = Assert.IsType<DBusVariant>(result.Value);
        Assert.Equal("nested", innerResult.Value);
    }

    [TmdsInteropFact]
    public async Task TripleNestedVariant_RoundTrips()
    {
        var v1 = new DBusVariant(true);
        var v2 = new DBusVariant(v1);
        var v3 = new DBusVariant(v2);

        var reply = await EchoAsync(v3);
        var r3 = Assert.IsType<DBusVariant>(reply.Body[0]);
        var r2 = Assert.IsType<DBusVariant>(r3.Value);
        var r1 = Assert.IsType<DBusVariant>(r2.Value);
        Assert.Equal(true, r1.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Array_RoundTrips()
    {
        var variant = new DBusVariant(new List<string> { "a", "b", "c" });

        var reply = await EchoAsync(variant);
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        var list = Assert.IsAssignableFrom<IList<string>>(result.Value);
        Assert.Equal(["a", "b", "c"], list);
    }

    [TmdsInteropFact]
    public async Task Variant_Dict_RoundTrips()
    {
        var dict = new Dictionary<string, int> { ["x"] = 1, ["y"] = 2 };
        var variant = new DBusVariant(dict);

        var reply = await EchoAsync(variant);
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        var resultDict = Assert.IsAssignableFrom<IDictionary<string, int>>(result.Value);
        Assert.Equal(1, resultDict["x"]);
        Assert.Equal(2, resultDict["y"]);
    }

    [TmdsInteropFact]
    public async Task Variant_Struct_RoundTrips()
    {
        var s = new DBusStruct(["hello", 42, true]);
        var variant = new DBusVariant(s, "(sib)");

        var reply = await EchoAsync(variant);
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        var resultStruct = Assert.IsType<DBusStruct>(result.Value);
        Assert.Equal("hello", resultStruct[0]);
        Assert.Equal(42, resultStruct[1]);
        Assert.Equal(true, resultStruct[2]);
    }

    [TmdsInteropFact]
    public async Task Variant_ObjectPath_RoundTrips()
    {
        var variant = new DBusVariant(new DBusObjectPath("/a/b/c"));

        var reply = await EchoAsync(variant);
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        var path = Assert.IsType<DBusObjectPath>(result.Value);
        Assert.Equal("/a/b/c", path.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Byte_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant((byte)127));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal((byte)127, result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Double_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant(2.71828));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(2.71828, result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_Int64_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant(long.MaxValue));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(long.MaxValue, result.Value);
    }

    [TmdsInteropFact]
    public async Task Variant_UInt64_RoundTrips()
    {
        var reply = await EchoAsync(new DBusVariant(ulong.MaxValue));
        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(ulong.MaxValue, result.Value);
    }

    // ==================== Container Round-Trips ====================

    [TmdsInteropFact]
    public async Task Array_Strings_RoundTrips()
    {
        var reply = await EchoAsync(new List<string> { "alpha", "beta", "gamma" });
        var result = Assert.IsAssignableFrom<IList<string>>(reply.Body[0]);
        Assert.Equal(["alpha", "beta", "gamma"], result);
    }

    [TmdsInteropFact]
    public async Task Array_Ints_RoundTrips()
    {
        var reply = await EchoAsync(new List<int> { 1, 2, 3, 4, 5 });
        var result = Assert.IsAssignableFrom<IList<int>>(reply.Body[0]);
        Assert.Equal([1, 2, 3, 4, 5], result);
    }

    [TmdsInteropFact]
    public async Task Array_Bytes_RoundTrips()
    {
        var reply = await EchoAsync(new List<byte> { 0x00, 0xFF, 0x42 });
        var result = Assert.IsAssignableFrom<IList<byte>>(reply.Body[0]);
        Assert.Equal([0x00, 0xFF, 0x42], result);
    }

    [TmdsInteropFact]
    public async Task NestedArrays_RoundTrips()
    {
        var nested = new List<List<string>>
        {
            new() { "a", "b" },
            new() { "c", "d", "e" },
        };

        var reply = await EchoAsync(nested);
        // Different transports may produce List<List<string>> or List<object> with inner List<string>
        if (reply.Body[0] is IList<List<string>> typed)
        {
            Assert.Equal(2, typed.Count);
            Assert.Equal(["a", "b"], typed[0]);
            Assert.Equal(["c", "d", "e"], typed[1]);
        }
        else
        {
            var boxed = Assert.IsAssignableFrom<IList<object>>(reply.Body[0]);
            Assert.Equal(2, boxed.Count);
            Assert.Equal(["a", "b"], Assert.IsAssignableFrom<IList<string>>(boxed[0]));
            Assert.Equal(["c", "d", "e"], Assert.IsAssignableFrom<IList<string>>(boxed[1]));
        }
    }

    [TmdsInteropFact]
    public async Task Dict_StringString_RoundTrips()
    {
        var dict = new Dictionary<string, string> { ["key1"] = "val1", ["key2"] = "val2" };

        var reply = await EchoAsync(dict);
        var result = Assert.IsAssignableFrom<IDictionary<string, string>>(reply.Body[0]);
        Assert.Equal("val1", result["key1"]);
        Assert.Equal("val2", result["key2"]);
    }

    [TmdsInteropFact]
    public async Task Dict_StringInt_RoundTrips()
    {
        var dict = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var reply = await EchoAsync(dict);
        var result = Assert.IsAssignableFrom<IDictionary<string, int>>(reply.Body[0]);
        Assert.Equal(1, result["a"]);
        Assert.Equal(2, result["b"]);
    }

    [TmdsInteropFact]
    public async Task Dict_StringVariant_RoundTrips()
    {
        // a{sv} — the most common D-Bus pattern (property bags)
        var dict = new Dictionary<string, DBusVariant>
        {
            ["name"] = new DBusVariant("test"),
            ["count"] = new DBusVariant(42),
            ["enabled"] = new DBusVariant(true),
        };

        var reply = await EchoAsync(dict);
        // Different transports may produce Dict<string, object> or Dict<string, DBusVariant>
        if (reply.Body[0] is IDictionary<string, object> objDict)
        {
            var nameVar = Assert.IsType<DBusVariant>(objDict["name"]);
            Assert.Equal("test", nameVar.Value);
            var countVar = Assert.IsType<DBusVariant>(objDict["count"]);
            Assert.Equal(42, countVar.Value);
        }
        else
        {
            var varDict = Assert.IsAssignableFrom<IDictionary<string, DBusVariant>>(reply.Body[0]);
            Assert.Equal("test", varDict["name"].Value);
            Assert.Equal(42, varDict["count"].Value);
        }
    }

    [TmdsInteropFact]
    public async Task Struct_Mixed_RoundTrips()
    {
        var s = new DBusStruct(["hello", 42, true]);

        var reply = await EchoAsync(s);
        var result = Assert.IsType<DBusStruct>(reply.Body[0]);
        Assert.Equal("hello", result[0]);
        Assert.Equal(42, result[1]);
        Assert.Equal(true, result[2]);
    }

    [TmdsInteropFact]
    public async Task Array_OfStructs_RoundTrips()
    {
        var structs = new List<DBusStruct>
        {
            new(["a", 1]),
            new(["b", 2]),
        };

        var reply = await EchoAsync(structs);
        var result = Assert.IsAssignableFrom<IList<DBusStruct>>(reply.Body[0]);
        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0][0]);
        Assert.Equal(1, result[0][1]);
        Assert.Equal("b", result[1][0]);
        Assert.Equal(2, result[1][1]);
    }

    // ==================== Multi-Arg & Wire-Level Round-Trips ====================

    [TmdsInteropFact]
    public async Task EchoAll_MultipleArgs_DifferentTypes()
    {
        var reply = await EchoAllAsync("hello", 42, true, 3.14);
        Assert.Equal(4, reply.Body.Count);
        Assert.Equal("hello", reply.Body[0]);
        Assert.Equal(42, reply.Body[1]);
        Assert.Equal(true, reply.Body[2]);
        Assert.Equal(3.14, reply.Body[3]);
    }

    [TmdsInteropFact]
    public async Task EchoAll_AllPrimitiveTypes()
    {
        var reply = await EchoAllAsync(
            (byte)42, true, (short)-1234, (ushort)5678,
            -100000, 100000u, -9876543210L, 9876543210UL,
            3.14159, "hello", new DBusObjectPath("/test"));

        Assert.Equal(11, reply.Body.Count);
        Assert.Equal((byte)42, reply.Body[0]);
        Assert.Equal(true, reply.Body[1]);
        Assert.Equal((short)-1234, reply.Body[2]);
        Assert.Equal((ushort)5678, reply.Body[3]);
        Assert.Equal(-100000, reply.Body[4]);
        Assert.Equal(100000u, reply.Body[5]);
        Assert.Equal(-9876543210L, reply.Body[6]);
        Assert.Equal(9876543210UL, reply.Body[7]);
        Assert.Equal(3.14159, reply.Body[8]);
        Assert.Equal("hello", reply.Body[9]);
        Assert.IsType<DBusObjectPath>(reply.Body[10]);
        Assert.Equal("/test", ((DBusObjectPath)reply.Body[10]).Value);
    }

    [TmdsInteropFact]
    public async Task EchoAll_MixedContainersAndPrimitives()
    {
        var list = new List<int> { 10, 20, 30 };
        var variant = new DBusVariant("in-variant");
        var dict = new Dictionary<string, string> { ["k"] = "v" };

        var reply = await EchoAllAsync("header", list, variant, dict, 99);

        Assert.Equal(5, reply.Body.Count);
        Assert.Equal("header", reply.Body[0]);
        Assert.Equal([10, 20, 30], Assert.IsAssignableFrom<IList<int>>(reply.Body[1]));
        var v = Assert.IsType<DBusVariant>(reply.Body[2]);
        Assert.Equal("in-variant", v.Value);
        var d = Assert.IsAssignableFrom<IDictionary<string, string>>(reply.Body[3]);
        Assert.Equal("v", d["k"]);
        Assert.Equal(99, reply.Body[4]);
    }

    [TmdsInteropFact]
    public async Task EchoAll_EmptyBody()
    {
        var reply = await EchoAllAsync();
        Assert.Empty(reply.Body);
    }

    [TmdsInteropFact]
    public async Task Signal_StringBody()
    {
        var name = TestName();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);

        await using var client = await CreateClientConnectionAsync();

        string? received = null;
        var signalReceived = new SemaphoreSlim(0);

        using var sub = await client.SubscribeAsync(
            null, EchoPath, EchoInterface, "Notify",
            msg =>
            {
                if (msg.Body.Count > 0 && msg.Body[0] is string s)
                    received = s;
                signalReceived.Release();
                return Task.CompletedTask;
            });

        await server.SendMessageAsync(
            DBusMessage.CreateSignal(EchoPath, EchoInterface, "Notify", "payload"));

        Assert.True(await signalReceived.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("payload", received);
    }

    [TmdsInteropFact]
    public async Task Signal_VariantBody()
    {
        var name = TestName();

        await using var server = await CreateServerConnectionAsync();
        await server.RequestNameAsync(name);

        await using var client = await CreateClientConnectionAsync();

        DBusVariant? received = null;
        var signalReceived = new SemaphoreSlim(0);

        using var sub = await client.SubscribeAsync(
            null, EchoPath, EchoInterface, "Changed",
            msg =>
            {
                if (msg.Body.Count > 0 && msg.Body[0] is DBusVariant v)
                    received = v;
                signalReceived.Release();
                return Task.CompletedTask;
            });

        await server.SendMessageAsync(
            DBusMessage.CreateSignal(EchoPath, EchoInterface, "Changed",
                new DBusVariant(42)));

        Assert.True(await signalReceived.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.NotNull(received);
        Assert.Equal(42, received!.Value);
    }
}
