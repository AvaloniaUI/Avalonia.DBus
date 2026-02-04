namespace Avalonia.DBus;

public enum DBusRequestNameReply
{
    /// <summary>
    /// The caller is now the primary owner of the name.
    /// </summary>
    PrimaryOwner = 1,

    /// <summary>
    /// The caller is in the queue to own the name.
    /// </summary>
    InQueue = 2,

    /// <summary>
    /// The name already has an owner and DoNotQueue was specified.
    /// </summary>
    Exists = 3,

    /// <summary>
    /// The caller already owns the name.
    /// </summary>
    AlreadyOwner = 4
}
