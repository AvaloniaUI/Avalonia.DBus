using System;
using System.Threading.Tasks;
using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

public sealed class BusFixture : IAsyncLifetime
{
    public DBusConnection? Connection { get; private set; }

    /// <summary>
    /// Returns the connection, throwing if not available.
    /// Call this from tests guarded by [IntegrationFact].
    /// </summary>
    public DBusConnection RequireConnection()
    {
        return Connection ?? throw new InvalidOperationException(
            "D-Bus session bus is not available. Tests using this should be guarded by [IntegrationFact].");
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS")))
            return;

        try
        {
            Connection = await DBusConnection.ConnectSessionAsync();
        }
        catch
        {
            // D-Bus not available - integration tests will be skipped via attribute
        }
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
            await Connection.DisposeAsync();
    }
}
