using Xunit;

namespace Avalonia.DBus.Testing;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (DbusDaemonFixture.FindDbusDaemon() is null)
            Skip = "dbus-daemon binary not found on this system.";
    }
}
