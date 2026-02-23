using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Contracts;
using Avalonia.DBus.Interop.Tests.Helpers;
using NDesk.DBus;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.DBus.Interop.Tests.NdeskServerTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class ComplexTypeTests(InteropFixture fixture, ITestOutputHelper output)
{
    private const string TypeTestInterface = "org.avalonia.dbus.interop.TypeTest";
    private static readonly DBusObjectPath TypeTestPath = "/org/avalonia/dbus/interop/TypeTest";
    private static readonly ObjectPath NdeskTypeTestPath = new("/org/avalonia/dbus/interop/TypeTest");

    private static string TestName() => $"org.avalonia.dbus.interop.typetest.t{Guid.NewGuid():N}";

    private async Task<DBusMessage> CallTypeTestAsync(
        DBusConnection conn,
        string destination,
        string member,
        CancellationToken ct,
        params object[] args)
    {
        return await conn.CallMethodAsync(
            destination, TypeTestPath, TypeTestInterface, member, ct, args);
    }

    [InteropFact]
    public async Task GetStringArray_ReturnsListOfStrings()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetStringArray", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<List<string>>(reply.Body[0]);
        Assert.Equal(["alpha", "beta", "gamma"], result);
    }

    [InteropFact]
    public async Task GetIntArray_ReturnsListOfInts()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetIntArray", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<List<int>>(reply.Body[0]);
        Assert.Equal([10, 20, 30, 40], result);
    }

    [InteropFact]
    public async Task SumArray_AvaloniaPassesArrayToNdesk()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(
            conn, name, "SumArray", CancellationToken.None,
            new List<int> { 1, 2, 3, 4, 5 });

        Assert.NotEmpty(reply.Body);
        Assert.Equal(15, (int)reply.Body[0]);
    }

    [InteropFact]
    public async Task JoinStrings_AvaloniaPassesArrayAndString()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(
            conn, name, "JoinStrings", CancellationToken.None,
            new List<string> { "a", "b", "c" }, "-");

        Assert.NotEmpty(reply.Body);
        Assert.Equal("a-b-c", (string)reply.Body[0]);
    }

    [InteropFact]
    public async Task GetStringMap_ReturnsDictionary()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetStringMap", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<Dictionary<string, string>>(reply.Body[0]);
        Assert.Equal(new Dictionary<string, string> { ["name"] = "test", ["version"] = "1.0" }, result);
    }

    [InteropFact]
    public async Task LookupInMap_PassesStringReturnsString()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(
            conn, name, "LookupInMap", CancellationToken.None, "name");

        Assert.NotEmpty(reply.Body);
        Assert.Equal("test", (string)reply.Body[0]);
    }

    [InteropFact]
    public async Task GetTuple_ReturnsStruct()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetTuple", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<DBusStruct>(reply.Body[0]);
        Assert.Equal("hello", (string)result[0]);
        Assert.Equal("world", (string)result[1]);
    }

    [InteropFact]
    public async Task GetVariantString_ReturnsDBusVariant()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetVariantString", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal("variant-string", (string)result.Value);
    }

    [InteropFact]
    public async Task GetVariantInt_ReturnsDBusVariant()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetVariantInt", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<DBusVariant>(reply.Body[0]);
        Assert.Equal(42, (int)result.Value);
    }

    [InteropFact]
    public async Task GetMixedMap_ReturnsDictWithVariantValues()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        var reply = await CallTypeTestAsync(conn, name, "GetMixedMap", CancellationToken.None);
        Assert.NotEmpty(reply.Body);

        var result = Assert.IsType<Dictionary<string, DBusVariant>>(reply.Body[0]);
        Assert.Equal(3, (int)result["count"].Value);
        Assert.Equal("mixed", (string)result["label"].Value);
    }

    [InteropFact]
    public async Task NotifySignal_CarriesVariantPayload()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskTypeTestPath, new TypeTestService());
        using var runner = new NdeskServerRunner(serverBus);

        string? receivedTag = null;
        DBusVariant? receivedPayload = null;
        var signalReceived = new SemaphoreSlim(0);

        using var sub = await conn.SubscribeAsync(
            null, TypeTestPath, TypeTestInterface, "Notify",
            msg =>
            {
                receivedTag = (string)msg.Body[0];
                receivedPayload = msg.Body[1] as DBusVariant;
                signalReceived.Release();
                return Task.CompletedTask;
            });

        await CallTypeTestAsync(
            conn, name, "FireNotify", CancellationToken.None,
            "test-tag", new DBusVariant("hello-payload"));

        Assert.True(
            await signalReceived.WaitAsync(TimeSpan.FromSeconds(5)),
            "Timed out waiting for Notify signal");

        Assert.Equal("test-tag", receivedTag);
        Assert.NotNull(receivedPayload);
        Assert.Equal("hello-payload", (string)receivedPayload!.Value);
    }
}
