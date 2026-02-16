using System.Linq;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class ConnectionTests(BusFixture fixture)
{
    [IntegrationFact]
    public async Task PrivateDaemon_HasNoExternalNames()
    {
        var connection = fixture.RequireConnection();

        var names = await connection.ListNamesAsync();

        // The only well-known name on a fresh private bus is the daemon itself.
        var wellKnown = names.Where(n => !n.StartsWith(":")).ToList();
        Assert.Single(wellKnown);
        Assert.Equal("org.freedesktop.DBus", wellKnown[0]);
    }

    [IntegrationFact]
    public async Task ConnectSessionAsync_Succeeds_UniqueName_Assigned()
    {
        var connection = fixture.RequireConnection();
        var name = await connection.GetUniqueNameAsync();

        Assert.NotNull(name);
        Assert.StartsWith(":", name);
    }

    [IntegrationFact]
    public async Task DisposeAsync_DisconnectsCleanly()
    {
        var connection = await fixture.CreateConnectionAsync();
        var name = await connection.GetUniqueNameAsync();
        Assert.NotNull(name);

        await connection.DisposeAsync();
    }

    [IntegrationFact]
    public async Task DoubleDispose_IsSafe()
    {
        var connection = await fixture.CreateConnectionAsync();
        await connection.DisposeAsync();
        await connection.DisposeAsync();
    }

}
