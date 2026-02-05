namespace Avalonia.DBus;

/// <summary>
/// Represents a Unix file descriptor passed over D-Bus.
/// </summary>
public record DBusUnixFd(int Fd);