namespace Avalonia.DBus.Native;

[NativeTypeName("unsigned int")]
internal enum DBusHandlerResult : uint
{
    DBUS_HANDLER_RESULT_HANDLED,
    DBUS_HANDLER_RESULT_NOT_YET_HANDLED,
    DBUS_HANDLER_RESULT_NEED_MEMORY,
}