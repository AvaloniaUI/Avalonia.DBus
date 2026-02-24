using System.IO;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// Serializes and deserializes <see cref="DBusMessage"/> instances to and from streams.
/// </summary>
internal interface IDBusMessageSerializer
{
    void Serialize(DBusMessage message, Stream stream);
    DBusMessage Deserialize(Stream stream);
}
