using System;

namespace Avalonia.DBus.Wire;

/// <summary>
/// Represents a D-Bus object path.
/// </summary>
public readonly struct DBusObjectPath : IEquatable<DBusObjectPath>
{
    public DBusObjectPath(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }

    public static implicit operator string(DBusObjectPath path) => path.Value;

    public static explicit operator DBusObjectPath(string value) => new(value);

    public bool Equals(DBusObjectPath other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DBusObjectPath other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(DBusObjectPath left, DBusObjectPath right) => left.Equals(right);

    public static bool operator !=(DBusObjectPath left, DBusObjectPath right) => !left.Equals(right);
}
