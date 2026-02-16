using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

public sealed class BusFixture : IAsyncLifetime
{
    private readonly DbusDaemonFixture _daemon = new();

    public DBusConnection? Connection { get; private set; }

    public string? DaemonAddress => _daemon.Address;

    /// <summary>
    /// Returns the connection, throwing if not available.
    /// Call this from tests guarded by [IntegrationFact].
    /// </summary>
    public DBusConnection RequireConnection()
    {
        return Connection ?? throw new InvalidOperationException(
            "D-Bus session bus is not available. Tests using this should be guarded by [IntegrationFact].");
    }

    /// <summary>
    /// Creates an additional connection to the test daemon.
    /// Caller is responsible for disposing the returned connection.
    /// </summary>
    public async Task<DBusConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        if (_daemon.Address is null)
            throw new InvalidOperationException("D-Bus daemon is not available.");

        return await DBusConnection.ConnectAsync(_daemon.Address, ct);
    }

    public async Task InitializeAsync()
    {
        await _daemon.InitializeAsync();

        if (_daemon.Address is null)
            return;

        try
        {
            Connection = await DBusConnection.ConnectAsync(_daemon.Address);
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

        await _daemon.DisposeAsync();
    }
}
