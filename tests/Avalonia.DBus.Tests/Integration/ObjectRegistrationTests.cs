using System;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class ObjectRegistrationTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task CallUnknownObject_ReturnsError()
    {
        var connection = fixture.RequireConnection();
        var myName = await connection.GetUniqueNameAsync();
        Assert.NotNull(myName);

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                myName!,
                (DBusObjectPath)"/nonexistent/object/path",
                "org.test.Iface",
                "Method"));

        Assert.Contains("UnknownObject", ex.ErrorName);
    }

    [IntegrationFact]
    public void RegisterObjects_NullTargets_Throws()
    {
        var connection = fixture.RequireConnection();
        Assert.Throws<ArgumentNullException>(() =>
            connection.RegisterObjects(
                (DBusObjectPath)"/test",
                null!));
    }

    [IntegrationFact]
    public void RegisterObjects_EmptyTargets_Throws()
    {
        var connection = fixture.RequireConnection();
        Assert.Throws<InvalidOperationException>(() =>
            connection.RegisterObjects(
                (DBusObjectPath)"/test",
                []));
    }
}
