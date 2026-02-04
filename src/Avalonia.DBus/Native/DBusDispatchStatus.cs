namespace Avalonia.DBus.Native;

[NativeTypeName("unsigned int")]
internal enum DBusDispatchStatus : uint
{
    DBUS_DISPATCH_DATA_REMAINS,
    DBUS_DISPATCH_COMPLETE,
    DBUS_DISPATCH_NEED_MEMORY,
}