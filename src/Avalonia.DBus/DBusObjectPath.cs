using System;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus object path.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
readonly struct DBusObjectPath(string value) : IEquatable<DBusObjectPath>
{
    public string Value { get; } = value;

    public static implicit operator string(DBusObjectPath path) => path.Value;

    public static implicit operator DBusObjectPath(string value) => new(value);

    public bool Equals(DBusObjectPath other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DBusObjectPath other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(DBusObjectPath left, DBusObjectPath right) => left.Equals(right);

    public static bool operator !=(DBusObjectPath left, DBusObjectPath right) => !left.Equals(right);
}
