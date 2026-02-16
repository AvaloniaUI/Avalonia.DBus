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

    [IntegrationFact]
    public async Task NewConnection_GetsDistinctUniqueName()
    {
        await using var conn1 = await DBusConnection.ConnectSessionAsync();
        await using var conn2 = await DBusConnection.ConnectSessionAsync();

        var name1 = await conn1.GetUniqueNameAsync();
        var name2 = await conn2.GetUniqueNameAsync();

        Assert.NotNull(name1);
        Assert.NotNull(name2);
        Assert.NotEqual(name1, name2);
    }
}
