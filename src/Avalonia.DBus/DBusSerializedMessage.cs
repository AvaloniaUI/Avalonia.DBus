namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus message in wire format together with any ancillary Unix file descriptors
/// that were transported outside the byte payload.
/// </summary>
/// <param name="Message">The serialized D-Bus message bytes, including the header and body.</param>
/// <param name="Fds">Ancillary Unix file descriptors referenced by the serialized payload.</param>
#if !AVDBUS_INTERNAL
public
#endif
record DBusSerializedMessage(byte[] Message, int[] Fds);
