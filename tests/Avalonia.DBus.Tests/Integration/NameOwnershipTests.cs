using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class NameOwnershipTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task RequestName_Succeeds()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testName = $"org.avalonia.dbus.test.own.t{Guid.NewGuid():N}";

        try
        {
            var reply = await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
            Assert.Single(reply.Body);
            // 1 = DBUS_REQUEST_NAME_REPLY_PRIMARY_OWNER
            Assert.Equal(1u, reply.Body[0]);
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
    public async Task RequestName_GetNameOwner_Confirms()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testName = $"org.avalonia.dbus.test.getowner.t{Guid.NewGuid():N}";
        var myName = await connection.GetUniqueNameAsync();

        try
        {
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            var reply = await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "GetNameOwner",
                cts.Token,
                testName);

            Assert.Equal(myName, reply.Body[0]);
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
    public async Task ReleaseName_Succeeds()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testName = $"org.avalonia.dbus.test.release.t{Guid.NewGuid():N}";

        await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "RequestName",
            cts.Token,
            testName, 0u);

        var reply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ReleaseName",
            cts.Token,
            testName);

        Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
        Assert.Single(reply.Body);
        // 1 = DBUS_RELEASE_NAME_REPLY_RELEASED
        Assert.Equal(1u, reply.Body[0]);
    }

    [IntegrationFact]
    public async Task RequestName_WhenAlreadyTaken_ReturnsCorrectCode()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var testName = $"org.avalonia.dbus.test.taken.t{Guid.NewGuid():N}";

        await using var otherConn = await DBusConnection.ConnectSessionAsync();

        try
        {
            // First connection takes the name
            await otherConn.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            // Second connection tries to take it with DoNotQueue (4)
            var reply = await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 4u); // DBUS_NAME_FLAG_DO_NOT_QUEUE

            Assert.Equal(DBusMessageType.MethodReturn, reply.Type);
            // 3 = DBUS_REQUEST_NAME_REPLY_EXISTS
            Assert.Equal(3u, reply.Body[0]);
        }
        finally
        {
            try
            {
                await otherConn.CallMethodAsync(
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
}
