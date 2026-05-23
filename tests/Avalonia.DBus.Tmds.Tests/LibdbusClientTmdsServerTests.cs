using Avalonia.DBus.Tmds.Tests.Helpers;
using System.Threading.Tasks;
using Xunit;
namespace Avalonia.DBus.Tmds.Tests;

/// <summary>
/// Tests where a libdbus-backed DBusConnection acts as client,
/// calling methods on a Tmds-backed DBusConnection server.
/// All shared test cases are inherited from <see cref="InteropTestsBase"/>.
/// </summary>
[Collection(TmdsInteropTestCollection.Name)]
[Trait("Category", "TmdsInterop")]
public class LibdbusToTmdsTests(TmdsInteropFixture fixture)
    : InteropTestsBase(fixture)
{
    protected override Task<DBusConnection> CreateClientConnectionAsync()
        => Fixture.CreateLibdbusConnectionAsync();

    protected override Task<DBusConnection> CreateServerConnectionAsync()
        => Fixture.CreateTmdsConnectionAsync();
}
