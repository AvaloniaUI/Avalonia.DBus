using Avalonia.DBus.AutoGen;
using Avalonia.DBus.SourceGen;
using Avalonia.DBus.Wire;

namespace SourceGenHelloWorld;

internal static class Program
{
    private static async Task Main()
    {
        using var connection = new Connection(DBusBusType.DBUS_BUS_SESSION);
        var proxy = new OrgFreedesktopDBusProxy(connection, "org.freedesktop.DBus", "/org/freedesktop/DBus");
        var names = await proxy.ListNamesAsync();
        Console.WriteLine($"Got {names.Length} names from the bus.");
    }
}
