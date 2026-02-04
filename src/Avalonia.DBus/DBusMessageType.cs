namespace Avalonia.DBus;

public enum DBusMessageType
{
    Invalid = 0,
    MethodCall = 1,
    MethodReturn = 2,
    Error = 3,
    Signal = 4
}
