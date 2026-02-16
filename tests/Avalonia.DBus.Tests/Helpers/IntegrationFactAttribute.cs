using System;
using Xunit;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// Fact attribute that skips the test if no D-Bus session bus is available.
/// </summary>
public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (!DBusSessionAvailable())
            Skip = "D-Bus session bus is not available (set DBUS_SESSION_BUS_ADDRESS or run dbus-daemon).";
    }

    private static bool DBusSessionAvailable()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"));
    }
}