using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class MethodCallTests(BusFixture fixture)
{
    [IntegrationFact]
    public async Task Ping_Succeeds()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Ping is on org.freedesktop.DBus.Peer, not on the main interface proxy
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus.Peer",
            "Ping",
            cts.Token);

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
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
            await connection.ListNamesAsync(cts.Token));
    }

    [IntegrationFact]
    public async Task MultipleConcurrentCalls_AllComplete()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        const int count = 10;

        var tasks = Enumerable.Range(0, count)
            .Select(_ => connection.GetIdAsync(cts.Token))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(count, results.Length);
        Assert.All(results, id => Assert.NotEmpty(id));

        // All should return the same bus ID
        Assert.All(results, id => Assert.Equal(results[0], id));
    }

    [IntegrationFact]
    public async Task ListNames_ReturnsArrayContainingBusDaemon()
    {
        var connection = fixture.RequireConnection();

        var names = await connection.ListNamesAsync();

        Assert.NotNull(names);
        Assert.Contains("org.freedesktop.DBus", names);
    }
}
