using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class ObjectRegistrationTests(BusFixture fixture)
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

}
