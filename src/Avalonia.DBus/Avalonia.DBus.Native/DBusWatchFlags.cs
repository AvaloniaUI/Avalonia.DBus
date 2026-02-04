using System;

namespace Avalonia.DBus.AutoGen;

[NativeTypeName("unsigned int")]
[Flags]
internal enum DBusWatchFlags : uint
{
    DBUS_WATCH_READABLE = 1 << 0,
    DBUS_WATCH_WRITABLE = 1 << 1,
    DBUS_WATCH_ERROR = 1 << 2,
    DBUS_WATCH_HANGUP = 1 << 3,
}