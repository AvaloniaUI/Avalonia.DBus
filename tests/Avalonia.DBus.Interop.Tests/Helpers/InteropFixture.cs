using System;
using System.Threading;
using System.Threading.Tasks;
using NDesk.DBus;
using Xunit;

namespace Avalonia.DBus.Interop.Tests.Helpers;

public sealed class InteropFixture : IAsyncLifetime
{
    private readonly DbusDaemonFixture _daemon = new();
    private NdeskServerRunner? _ndeskPump;

    public DBusConnection? AvaloniaConnection { get; private set; }
    public Bus? NdeskBus { get; private set; }

    public string? DaemonAddress => _daemon.Address;

    public DBusConnection RequireAvaloniaConnection()
    {
        return AvaloniaConnection ?? throw new InvalidOperationException(
            "Avalonia.DBus connection is not available. Tests using this should be guarded by [IntegrationFact].");
    }

    public Bus RequireNdeskBus()
    {
        return NdeskBus ?? throw new InvalidOperationException(
            "NDesk.DBus bus is not available. Tests using this should be guarded by [IntegrationFact].");
    }

    public async Task<DBusConnection> CreateAvaloniaConnectionAsync(CancellationToken ct = default)
    {
        if (_daemon.Address is null)
            throw new InvalidOperationException("D-Bus daemon is not available.");

        return await DBusConnection.ConnectAsync(_daemon.Address, ct);
    }

    public Bus CreateNdeskBus()
    {
        if (_daemon.Address is null)
            throw new InvalidOperationException("D-Bus daemon is not available.");

        return new Bus(_daemon.Address);
    }

    public async Task InitializeAsync()
    {
        await _daemon.InitializeAsync();

        if (_daemon.Address is null)
            return;

        try
        {
            AvaloniaConnection = await DBusConnection.ConnectAsync(_daemon.Address);
        }
        catch
        {
            // Avalonia.DBus connection failed — integration tests will be skipped
        }

        try
        {
            NdeskBus = new Bus(_daemon.Address);
            // Start a background pump so that proxy calls from any thread
            // (including async continuations) work correctly. NDesk's
            // PendingCall uses Monitor.Wait when the caller is not on
            // mainThread — the pump reads replies and dispatches them.
            _ndeskPump = new NdeskServerRunner(NdeskBus);
        }
        catch
        {
            // NDesk.DBus connection failed — integration tests will be skipped
        }
    }

    public async Task DisposeAsync()
    {
        _ndeskPump?.Dispose();

        if (AvaloniaConnection is not null)
            await AvaloniaConnection.DisposeAsync();

        await _daemon.DisposeAsync();
    }
}
