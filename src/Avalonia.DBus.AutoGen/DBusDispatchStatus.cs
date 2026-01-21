namespace Avalonia.DBus.AutoGen;

[NativeTypeName("unsigned int")]
public enum DBusDispatchStatus : uint
{
    DBUS_DISPATCH_DATA_REMAINS,
    DBUS_DISPATCH_COMPLETE,
    DBUS_DISPATCH_NEED_MEMORY,
}