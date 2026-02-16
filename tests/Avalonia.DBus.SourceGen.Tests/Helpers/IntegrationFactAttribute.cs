using Xunit;

namespace Avalonia.DBus.SourceGen.Tests.Helpers;

/// <summary>
/// Fact attribute that skips the test if dbus-daemon is not found on the system.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (DbusDaemonFixture.FindDbusDaemon() is null)
            Skip = "dbus-daemon binary not found on this system.";
    }
}
