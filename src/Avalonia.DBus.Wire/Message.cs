using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public readonly unsafe struct Message
{
    private readonly DBusMessage* _message;

    internal Message(DBusMessage* message)
    {
        _message = message;
    }

    internal DBusMessage* Handle => _message;

    public string InterfaceAsString => DbusHelpers.PtrToString(dbus.dbus_message_get_interface(_message));

    public string MemberAsString => DbusHelpers.PtrToString(dbus.dbus_message_get_member(_message));

    public string SignatureAsString => DbusHelpers.PtrToString(dbus.dbus_message_get_signature(_message));

    public string PathAsString => DbusHelpers.PtrToString(dbus.dbus_message_get_path(_message));

    public Reader GetBodyReader() => Reader.Create(_message);
}
