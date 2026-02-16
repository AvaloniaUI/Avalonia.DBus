using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class MethodCallTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task Ping_Succeeds()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus.Peer",
            "Ping",
            cts.Token);

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
    }

    [IntegrationFact]
    public async Task GetNameOwner_ReturnsOwner()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            cts.Token,
            "org.freedesktop.DBus");

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Single(reply.Body);
        Assert.Equal("org.freedesktop.DBus", reply.Body[0]);
    }

    [IntegrationFact]
    public async Task CallNonExistentDestination_ThrowsServiceUnknown()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                "org.avalonia.dbus.test.nonexistent.service",
                (DBusObjectPath)"/test",
                "org.test.Interface",
                "SomeMethod",
                cts.Token));

        Assert.Contains("ServiceUnknown", ex.ErrorName);
    }

    [IntegrationFact]
    public async Task CallNonExistentMethod_ThrowsUnknownMethod()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "ThisMethodDoesNotExist",
                cts.Token));

        Assert.Contains("UnknownMethod", ex.ErrorName);
    }

    [IntegrationFact]
    public async Task CancellationToken_CancelsPendingCall()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource();

        // Cancel immediately before the reply can arrive
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "ListNames",
                cts.Token));
    }

    [IntegrationFact]
    public async Task MultipleConcurrentCalls_AllComplete()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        const int count = 10;

        var tasks = Enumerable.Range(0, count)
            .Select(_ => connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "GetId",
                cts.Token))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(count, results.Length);
        Assert.All(results, r => Assert.Equal(DBusMessageType.MethodReturn, r.Type));

        // All should return the same bus ID
        var firstId = results[0].Body[0];
        Assert.All(results, r => Assert.Equal(firstId, r.Body[0]));
    }

    [IntegrationFact]
    public async Task GetId_ReturnsValidId()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetId",
            cts.Token);

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Single(reply.Body);
        var id = Assert.IsType<string>(reply.Body[0]);
        Assert.NotEmpty(id);
    }

    [IntegrationFact]
    public async Task ListNames_ReturnsArrayContainingBusDaemon()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ListNames",
            cts.Token);

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Single(reply.Body);

        // Should contain at least the bus daemon itself
        var names = reply.Body[0];
        Assert.NotNull(names);

        if (names is System.Collections.Generic.List<string> nameList)
            Assert.Contains("org.freedesktop.DBus", nameList);
        else if (names is string[] nameArray)
            Assert.Contains("org.freedesktop.DBus", nameArray);
    }
}
