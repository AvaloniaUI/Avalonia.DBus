using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Trait("Category", "Interop")]
public class ConcurrencyTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task ConcurrentMethodCalls_100_AllComplete()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        const int count = 100;
        var tasks = new Task<string>[count];

        for (var i = 0; i < count; i++)
        {
            tasks[i] = connection.GetIdAsync(cts.Token);
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(count, results.Length);
        Assert.All(results, id => Assert.NotEmpty(id));
    }

    [IntegrationFact]
    public async Task MultipleConnections_ConcurrentCalls()
    {
        const int connectionCount = 5;
        var connections = new DBusConnection[connectionCount];

        try
        {
            for (var i = 0; i < connectionCount; i++)
                connections[i] = await DBusConnection.ConnectSessionAsync();

            var tasks = connections
                .Select(c => c.GetNameOwnerAsync("org.freedesktop.DBus"))
                .ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(connectionCount, results.Length);
            Assert.All(results, owner => Assert.Equal("org.freedesktop.DBus", owner));
        }
        finally
        {
            foreach (var conn in connections)
                await conn.DisposeAsync();
        }
    }

    [IntegrationFact]
    public async Task RapidSubscribeUnsubscribe_NoErrors()
    {
        var connection = fixture.RequireConnection();
        const int cycles = 20;

        for (var i = 0; i < cycles; i++)
        {
            var sub = await connection.WatchNameOwnerChangedAsync(
                (_, _, _) => { },
                emitOnCapturedContext: false);

            sub.Dispose();
        }
    }
}
