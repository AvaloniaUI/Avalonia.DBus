using System.Runtime.CompilerServices;

namespace Avalonia.DBus;

internal static class FreedesktopDBusGeneratedPrivateImplementationDoNotTouch
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Register()
    {
        DBusInteropMetadataRegistry.Register(
            new DBusInteropMetadata
            {
                ClrType = typeof(OrgFreedesktopDBusProxy),
                InterfaceName = "org.freedesktop.DBus",
                CreateProxy = static (connection, destination, path, iface) =>
                    new OrgFreedesktopDBusProxy(connection, destination, path, iface)
            });
    }
#pragma warning restore CA2255
}