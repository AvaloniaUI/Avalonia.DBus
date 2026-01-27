using System;

namespace Avalonia.DBus.Wire;

[Flags]
public enum DBusRequestNameFlags
{
    None = 0,

    /// <summary>
    /// Allow other connections to take over this name.
    /// </summary>
    AllowReplacement = 0x1,

    /// <summary>
    /// Try to take over the name from the current owner.
    /// </summary>
    ReplaceExisting = 0x2,

    /// <summary>
    /// Don't queue for the name if it's already owned.
    /// </summary>
    DoNotQueue = 0x4
}
