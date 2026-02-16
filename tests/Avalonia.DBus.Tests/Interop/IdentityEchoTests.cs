using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

/// <summary>
/// Tests type round-trip via the D-Bus daemon. Uses the built-in
/// OrgFreedesktopDBusProxy and extension methods where possible.
/// </summary>
[Trait("Category", "Interop")]
public class IdentityEchoTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task RoundTrip_Boolean_ViaNameHasOwner()
    {
        var connection = fixture.RequireConnection();

        var hasOwner = await connection.NameHasOwnerAsync("org.freedesktop.DBus");

        Assert.True(hasOwner);
    }
}
