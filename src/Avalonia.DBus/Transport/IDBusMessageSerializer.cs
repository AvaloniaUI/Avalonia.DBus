using System.IO;

namespace Avalonia.DBus.Transport;

/// <summary>
/// Serializes and deserializes <see cref="DBusMessage"/> instances to and from streams.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
interface IDBusMessageSerializer
{
    void Serialize(DBusMessage message, Stream stream);
    DBusMessage Deserialize(Stream stream);
}
