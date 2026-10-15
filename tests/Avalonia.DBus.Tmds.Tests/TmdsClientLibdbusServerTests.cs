using Avalonia.DBus.Tmds.Tests.Helpers;
using System.Threading.Tasks;
using Xunit;
namespace Avalonia.DBus.Tmds.Tests;

/// <summary>
/// Tests where a Tmds-backed DBusConnection acts as client,
/// calling methods on a libdbus-backed DBusConnection server.
/// All shared test cases are inherited from <see cref="InteropTestsBase"/>.
/// </summary>
[Collection(TmdsInteropTestCollection.Name)]
[Trait("Category", "TmdsInterop")]
public class TmdsToLibdbusTests(TmdsInteropFixture fixture)
    : InteropTestsBase(fixture)
{
    protected override Task<DBusConnection> CreateClientConnectionAsync()
        => Fixture.CreateTmdsConnectionAsync();

    protected override Task<DBusConnection> CreateServerConnectionAsync()
        => Fixture.CreateLibdbusConnectionAsync();

    // --- Tmds-specific tests (not part of shared interop) ---

    [TmdsInteropFact]
    public async Task TmdsConnection_CanRequestName()
    {
        await using var tmds = await Fixture.CreateTmdsConnectionAsync();
        var name = TestName();
        var result = await tmds.RequestNameAsync(name);
        Assert.Equal(DBusRequestNameReply.PrimaryOwner, result);
    }

    [TmdsInteropFact]
    public async Task TmdsConnection_CanListNames()
    {
        await using var tmds = await Fixture.CreateTmdsConnectionAsync();
        var names = await tmds.ListNamesAsync();
        Assert.Contains("org.freedesktop.DBus", names);
    }

    [TmdsInteropFact]
    public async Task TmdsConnection_HasUniqueName()
    {
        await using var tmds = await Fixture.CreateTmdsConnectionAsync();
        var uniqueName = await tmds.GetUniqueNameAsync();
        Assert.NotNull(uniqueName);
        Assert.StartsWith(":", uniqueName);
    }
}
