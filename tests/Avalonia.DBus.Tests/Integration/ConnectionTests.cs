using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class ConnectionTests(BusFixture fixture) : IClassFixture<BusFixture>
{
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
        var connection = await DBusConnection.ConnectSessionAsync();
        var name = await connection.GetUniqueNameAsync();
        Assert.NotNull(name);

        await connection.DisposeAsync();
    }

    [IntegrationFact]
    public async Task DoubleDispose_IsSafe()
    {
        var connection = await DBusConnection.ConnectSessionAsync();
        await connection.DisposeAsync();
        await connection.DisposeAsync();
    }

}
