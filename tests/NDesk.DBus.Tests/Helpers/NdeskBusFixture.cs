using System;
using System.Threading.Tasks;
using Xunit;

namespace NDesk.DBus.Tests.Helpers;

public sealed class NdeskBusFixture : IAsyncLifetime
{
    private readonly DbusDaemonFixture _daemon = new();

    public Bus Bus { get; private set; }

    public string DaemonAddress => _daemon.Address;

    public Bus RequireBus()
    {
        return Bus ?? throw new InvalidOperationException(
            "D-Bus session bus is not available. Tests using this should be guarded by [IntegrationFact].");
    }

    public Bus CreateBus()
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
            Bus = new Bus(_daemon.Address);
        }
        catch
        {
            // D-Bus not available - integration tests will be skipped via attribute
        }
    }

    public async Task DisposeAsync()
    {
        await _daemon.DisposeAsync();
    }
}
