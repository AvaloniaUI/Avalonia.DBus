namespace Avalonia.DBus;

/// <summary>
/// Represents a Unix file descriptor passed over D-Bus.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
record DBusUnixFd(int Fd);
