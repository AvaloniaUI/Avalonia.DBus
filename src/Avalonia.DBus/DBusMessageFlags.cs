using System;

namespace Avalonia.DBus.Wire;

[Flags]
public enum DBusMessageFlags
{
    None = 0,
    NoReplyExpected = 0x1,
    NoAutoStart = 0x2,
    AllowInteractiveAuthorization = 0x4
}
