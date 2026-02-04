using System;

namespace Avalonia.DBus;

/// <summary>
/// Represents a Unix file descriptor passed over D-Bus.
/// </summary>
public readonly struct DBusUnixFd : IEquatable<DBusUnixFd>
{
    public DBusUnixFd(int fd)
    {
        Fd = fd;
    }

    public int Fd { get; }

    public bool Equals(DBusUnixFd other) => Fd == other.Fd;

    public override bool Equals(object? obj) => obj is DBusUnixFd other && Equals(other);

    public override int GetHashCode() => Fd.GetHashCode();

    public override string ToString() => Fd.ToString();

    public static bool operator ==(DBusUnixFd left, DBusUnixFd right) => left.Equals(right);

    public static bool operator !=(DBusUnixFd left, DBusUnixFd right) => !left.Equals(right);
}
