using System;
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
public class EchoMethodCallTests(InteropFixture fixture, ITestOutputHelper output)
{
    private const string EchoInterface = "org.avalonia.dbus.interop.Echo";
    private static readonly DBusObjectPath EchoPath = "/org/avalonia/dbus/interop/Echo";
    private static readonly ObjectPath NdeskEchoPath = new("/org/avalonia/dbus/interop/Echo");

    private static string TestName() => $"org.avalonia.dbus.interop.echo.t{Guid.NewGuid():N}";

    private async Task<T> CallEchoMethodAsync<T>(
        DBusConnection conn,
        string destination,
        string member,
        CancellationToken ct,
        params object[] args)
    {
        var reply = await conn.CallMethodAsync(
            destination, EchoPath, EchoInterface, member, ct, args);

        Assert.NotEmpty(reply.Body);
        return (T)reply.Body[0];
    }

    [InteropFact]
    public async Task Echo_ReturnsIdenticalString()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var result = await CallEchoMethodAsync<string>(conn, name, "Echo", CancellationToken.None, "hello");
        Assert.Equal("hello", result);
    }

    [InteropFact]
    public async Task Add_ReturnsSumOfTwoInts()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var result = await CallEchoMethodAsync<int>(conn, name, "Add", CancellationToken.None, 17, 25);
        Assert.Equal(42, result);
    }

    [InteropFact]
    public async Task Concat_ReturnsConcatenatedStrings()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var result = await CallEchoMethodAsync<string>(conn, name, "Concat", CancellationToken.None, "foo", "bar");
        Assert.Equal("foobar", result);
    }

    [InteropFact]
    public async Task Negate_ReturnsNegatedLong()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var result = await CallEchoMethodAsync<long>(conn, name, "Negate", CancellationToken.None, 42L);
        Assert.Equal(-42L, result);
    }

    [InteropFact]
    public async Task Echo_EmptyString_ReturnsEmpty()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var result = await CallEchoMethodAsync<string>(conn, name, "Echo", CancellationToken.None, "");
        Assert.Equal("", result);
    }

    [InteropFact]
    public async Task CallNonExistentMethod_ReturnsError()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var serverBus = fixture.CreateLoggedNdeskBus(output);
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskEchoPath, new EchoService());
        using var runner = new NdeskServerRunner(serverBus);

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
        {
            await conn.CallMethodAsync(
                name, EchoPath, EchoInterface, "NonExistentMethod", CancellationToken.None);
        });

        Assert.Contains("UnknownMethod", ex.ErrorName, StringComparison.OrdinalIgnoreCase);
    }
}
