namespace Avalonia.DBus;

/// <summary>
/// A D-Bus message in its serialized wire format, with out-of-band file descriptors.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
record DBusSerializedMessage(byte[] Message, int[] Fds);
