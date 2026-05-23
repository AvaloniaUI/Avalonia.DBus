using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Testing;
using Avalonia.DBus.Tmds;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.DBus.Tmds.Tests.Helpers;

/// <summary>
/// Shared fixture that creates both a Tmds-backed and libdbus-backed
/// DBusConnection on the same private dbus-daemon.
/// </summary>
public sealed class TmdsInteropFixture : IAsyncLifetime
{
    private readonly DbusDaemonFixture _daemon = new();

    public DBusConnection? TmdsConnection { get; private set; }
    public DBusConnection? LibdbusConnection { get; private set; }

    public string? DaemonAddress => _daemon.Address;

    public DBusConnection RequireTmdsConnection()
        => TmdsConnection ?? throw new InvalidOperationException(
            "Tmds-backed connection is not available.");

    public DBusConnection RequireLibdbusConnection()
        => LibdbusConnection ?? throw new InvalidOperationException(
            "Libdbus-backed connection is not available.");

    public async Task<DBusConnection> CreateTmdsConnectionAsync(CancellationToken ct = default)
    {
        if (_daemon.Address is null)
            throw new InvalidOperationException("D-Bus daemon is not available.");

        var wire = await TmdsDBusWireConnection.ConnectAsync(_daemon.Address, ct);
        return new DBusConnection(wire);
    }

    public async Task<DBusConnection> CreateLibdbusConnectionAsync(CancellationToken ct = default)
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

        // If the daemon started but connections fail, that's a real error — let it propagate
        var wire = await TmdsDBusWireConnection.ConnectAsync(_daemon.Address);
        TmdsConnection = new DBusConnection(wire);

        LibdbusConnection = await DBusConnection.ConnectAsync(_daemon.Address);
    }

    public async Task DisposeAsync()
    {
        if (TmdsConnection is not null)
            await TmdsConnection.DisposeAsync();
        if (LibdbusConnection is not null)
            await LibdbusConnection.DisposeAsync();
        await _daemon.DisposeAsync();
    }
}
