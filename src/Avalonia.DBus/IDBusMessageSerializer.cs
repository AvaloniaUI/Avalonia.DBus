namespace Avalonia.DBus;

/// <summary>
/// Converts between <see cref="DBusMessage"/> and <see cref="DBusSerializedMessage"/>.
/// </summary>
internal interface IDBusMessageSerializer
{
    DBusSerializedMessage Serialize(DBusMessage message);
    DBusMessage Deserialize(DBusSerializedMessage serialized);
}
