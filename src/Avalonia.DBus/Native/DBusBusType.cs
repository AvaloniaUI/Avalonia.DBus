namespace Avalonia.DBus.Native;

[NativeTypeName("unsigned int")]
internal enum DBusBusType : uint
{
    DBUS_BUS_SESSION,
    DBUS_BUS_SYSTEM,
    DBUS_BUS_STARTER,
}