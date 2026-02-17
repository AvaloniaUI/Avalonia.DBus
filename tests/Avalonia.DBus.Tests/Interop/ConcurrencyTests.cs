using System.Linq;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Interop")]
public class ConcurrencyTests(BusFixture fixture)
{
    [IntegrationFact]
    public async Task MultipleConnections_ConcurrentCalls()
    {
        const int connectionCount = 5;
        var connections = new DBusConnection[connectionCount];

        try
        {
            for (var i = 0; i < connectionCount; i++)
                connections[i] = await fixture.CreateConnectionAsync();

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

}
