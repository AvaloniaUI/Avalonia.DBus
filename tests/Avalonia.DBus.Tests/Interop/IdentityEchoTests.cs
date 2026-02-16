using System;
using System.Collections.Generic;
using System.Threading;
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
    public async Task RoundTrip_String_ViaGetNameOwner()
    {
        var connection = fixture.RequireConnection();

        var owner = await connection.GetNameOwnerAsync("org.freedesktop.DBus");

        Assert.NotNull(owner);
        Assert.Equal("org.freedesktop.DBus", owner);
    }

    [IntegrationFact]
    public async Task RoundTrip_UInt32_ViaRequestName()
    {
        var connection = fixture.RequireConnection();
        var testName = $"org.avalonia.dbus.test.echo.t{Guid.NewGuid():N}";

        try
        {
            var reply = await connection.RequestNameAsync(testName);

            Assert.Equal(DBusRequestNameReply.PrimaryOwner, reply);
        }
        finally
        {
            try { await connection.ReleaseNameAsync(testName); }
            catch { /* best-effort */ }
        }
    }

    [IntegrationFact]
    public async Task RoundTrip_StringArray_ViaListNames()
    {
        var connection = fixture.RequireConnection();

        var names = await connection.ListNamesAsync();

        Assert.NotNull(names);
        Assert.Contains("org.freedesktop.DBus", names);
    }

    [IntegrationFact]
    public async Task RoundTrip_Boolean_ViaNameHasOwner()
    {
        var connection = fixture.RequireConnection();

        var hasOwner = await connection.NameHasOwnerAsync("org.freedesktop.DBus");

        Assert.True(hasOwner);
    }

    [IntegrationFact]
    public async Task SignatureInference_MatchesWireFormat()
    {
        var msg = DBusMessage.CreateMethodCall(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            "org.freedesktop.DBus");

        Assert.Equal("s", msg.Signature.Value);

        var connection = fixture.RequireConnection();

        var owner = await connection.GetNameOwnerAsync("org.freedesktop.DBus");
        Assert.NotNull(owner);
    }
}
