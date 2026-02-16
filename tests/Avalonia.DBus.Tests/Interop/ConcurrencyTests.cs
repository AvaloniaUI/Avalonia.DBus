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
        var tasks = new Task<DBusMessage>[count];

        for (var i = 0; i < count; i++)
        {
            tasks[i] = connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "GetId",
                cts.Token);
        }

        var results = await Task.WhenAll(tasks);

        Assert.Equal(count, results.Length);
        Assert.All(results, result =>
            Assert.Equal(DBusMessageType.MethodReturn, result.Type));
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

            var tasks = connections.Select(c =>
                c.CallMethodAsync(
                    "org.freedesktop.DBus",
                    (DBusObjectPath)"/org/freedesktop/DBus",
                    "org.freedesktop.DBus",
                    "ListNames")).ToArray();

            var results = await Task.WhenAll(tasks);

            Assert.Equal(connectionCount, results.Length);
            Assert.All(results, result =>
                Assert.Equal(DBusMessageType.MethodReturn, result.Type));
        }
        finally
        {
            foreach (var conn in connections)
            {
                await conn.DisposeAsync();
            }
        }
    }

    [IntegrationFact]
    public async Task RapidSubscribeUnsubscribe_NoErrors()
    {
        var connection = fixture.RequireConnection();
        const int cycles = 20;

        for (var i = 0; i < cycles; i++)
        {
            var sub = await connection.SubscribeAsync(
                "org.freedesktop.DBus",
                null,
                "org.freedesktop.DBus",
                "NameOwnerChanged",
                _ => Task.CompletedTask);

            sub.Dispose();
        }
    }
}
