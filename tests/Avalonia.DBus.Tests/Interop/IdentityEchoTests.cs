using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

/// <summary>
/// Tests type round-trip via the D-Bus daemon. Uses the bus daemon's own methods
/// as a simulated echo service (passing typed arguments and verifying responses).
/// </summary>
[Trait("Category", "Interop")]
public class IdentityEchoTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task RoundTrip_String_ViaGetNameOwner()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        // GetNameOwner("org.freedesktop.DBus") -> "org.freedesktop.DBus"
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            cts.Token,
            "org.freedesktop.DBus");

        Assert.IsType<string>(reply.Body[0]);
        Assert.Equal("org.freedesktop.DBus", reply.Body[0]);
    }

    [IntegrationFact]
    public async Task RoundTrip_UInt32_ViaRequestName()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testName = $"org.avalonia.dbus.test.echo.t{Guid.NewGuid():N}";

        try
        {
            var reply = await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            // Reply body contains a uint32 reply code
            Assert.IsType<uint>(reply.Body[0]);
            Assert.Equal(1u, reply.Body[0]); // PRIMARY_OWNER
        }
        finally
        {
            try
            {
                await connection.CallMethodAsync(
                    "org.freedesktop.DBus",
                    (DBusObjectPath)"/org/freedesktop/DBus",
                    "org.freedesktop.DBus",
                    "ReleaseName",
                    cts.Token,
                    testName);
            }
            catch { }
        }
    }

    [IntegrationFact]
    public async Task RoundTrip_StringArray_ViaListNames()
    {
        var connection = fixture.RequireConnection();
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ListNames");

        // ListNames returns an array of strings
        Assert.Single(reply.Body);
        var names = reply.Body[0];
        Assert.NotNull(names);

        // Verify it contains at least org.freedesktop.DBus
        if (names is List<string> nameList)
        {
            Assert.Contains("org.freedesktop.DBus", nameList);
        }
        else if (names is string[] nameArray)
        {
            Assert.Contains("org.freedesktop.DBus", nameArray);
        }
    }

    [IntegrationFact]
    public async Task RoundTrip_Boolean_ViaNameHasOwner()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameHasOwner",
            cts.Token,
            "org.freedesktop.DBus");

        Assert.Single(reply.Body);
        Assert.IsType<bool>(reply.Body[0]);
        Assert.True((bool)reply.Body[0]);
    }

    [IntegrationFact]
    public async Task SignatureInference_MatchesWireFormat()
    {
        // The message we send should have its signature auto-inferred
        var msg = DBusMessage.CreateMethodCall(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            "org.freedesktop.DBus");

        Assert.Equal("s", msg.Signature.Value);

        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            cts.Token,
            "org.freedesktop.DBus");

        Assert.Equal("s", reply.Signature.Value);
    }
}
