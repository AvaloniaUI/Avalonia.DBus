namespace Avalonia.DBus;

/// <summary>
/// Converts between logical <see cref="DBusMessage"/> instances and serialized D-Bus wire payloads.
/// </summary>
internal interface IDBusMessageSerializer
{
    /// <summary>
    /// Serializes a message into D-Bus wire bytes plus any ancillary Unix file descriptors.
    /// </summary>
    /// <param name="message">The message to serialize.</param>
    /// <returns>The serialized payload for <paramref name="message"/>.</returns>
    DBusSerializedMessage Serialize(DBusMessage message);

    /// <summary>
    /// Deserializes a wire payload and its ancillary Unix file descriptors into a logical D-Bus message.
    /// </summary>
    /// <param name="serialized">The serialized message payload to deserialize.</param>
    /// <returns>The deserialized message.</returns>
    DBusMessage Deserialize(DBusSerializedMessage serialized);
}
