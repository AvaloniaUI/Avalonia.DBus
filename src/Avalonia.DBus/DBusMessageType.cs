namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
enum DBusMessageType
{
    Invalid = 0,
    MethodCall = 1,
    MethodReturn = 2,
    Error = 3,
    Signal = 4
}
