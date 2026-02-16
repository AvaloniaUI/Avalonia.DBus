using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class PropertyTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task GetProperty_InvalidInterface_ThrowsError()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var myName = await connection.GetUniqueNameAsync();
        Assert.NotNull(myName);

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                myName!,
                (DBusObjectPath)"/nonexistent/prop/path",
                "org.freedesktop.DBus.Properties",
                "Get",
                cts.Token,
                "org.nonexistent.Interface", "SomeProperty"));

        Assert.NotNull(ex.ErrorName);
    }

    [IntegrationFact]
    public async Task GetAllProperties_InvalidInterface_ThrowsError()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var myName = await connection.GetUniqueNameAsync();
        Assert.NotNull(myName);

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                myName!,
                (DBusObjectPath)"/nonexistent/prop/path",
                "org.freedesktop.DBus.Properties",
                "GetAll",
                cts.Token,
                "org.nonexistent.Interface"));

        Assert.NotNull(ex.ErrorName);
    }
}
