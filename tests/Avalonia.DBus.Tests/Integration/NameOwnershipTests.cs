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
        var testName = $"org.avalonia.dbus.test.own.t{Guid.NewGuid():N}";

        try
        {
            var reply = await connection.RequestNameAsync(testName, cancellationToken: Cts().Token);

            Assert.Equal(DBusRequestNameReply.PrimaryOwner, reply);
        }
        finally
        {
            await TryRelease(connection, testName);
        }
    }

    [IntegrationFact]
    public async Task RequestName_GetNameOwner_Confirms()
    {
        var connection = fixture.RequireConnection();
        var testName = $"org.avalonia.dbus.test.getowner.t{Guid.NewGuid():N}";
        var myName = await connection.GetUniqueNameAsync();

        try
        {
            await connection.RequestNameAsync(testName, cancellationToken: Cts().Token);

            var owner = await connection.GetNameOwnerAsync(testName, Cts().Token);

            Assert.Equal(myName, owner);
        }
        finally
        {
            await TryRelease(connection, testName);
        }
    }

    [IntegrationFact]
    public async Task ReleaseName_Succeeds()
    {
        var connection = fixture.RequireConnection();
        var testName = $"org.avalonia.dbus.test.release.t{Guid.NewGuid():N}";

        await connection.RequestNameAsync(testName, cancellationToken: Cts().Token);
        await connection.ReleaseNameAsync(testName, Cts().Token);

        // After release, name should have no owner
        var owner = await connection.GetNameOwnerAsync(testName, Cts().Token);
        Assert.Null(owner);
    }

    [IntegrationFact]
    public async Task RequestName_WhenAlreadyTaken_ReturnsExists()
    {
        var connection = fixture.RequireConnection();
        var testName = $"org.avalonia.dbus.test.taken.t{Guid.NewGuid():N}";

        await using var otherConn = await DBusConnection.ConnectSessionAsync();

        try
        {
            // First connection takes the name
            await otherConn.RequestNameAsync(testName, cancellationToken: Cts().Token);

            // Second connection tries with DoNotQueue
            var reply = await connection.RequestNameAsync(
                testName,
                DBusRequestNameFlags.DoNotQueue,
                Cts().Token);

            Assert.Equal(DBusRequestNameReply.Exists, reply);
        }
        finally
        {
            await TryRelease(otherConn, testName);
        }
    }

    private static CancellationTokenSource Cts() => new(TimeSpan.FromSeconds(10));

    private static async Task TryRelease(DBusConnection connection, string name)
    {
        try { await connection.ReleaseNameAsync(name); }
        catch { /* best-effort cleanup */ }
    }
}
