using System;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus type signature.
/// </summary>
public readonly struct DBusSignature : IEquatable<DBusSignature>
{
    public DBusSignature(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }

    public static implicit operator string(DBusSignature signature) => signature.Value;

    public static explicit operator DBusSignature(string value) => new(value);

    public bool Equals(DBusSignature other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is DBusSignature other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(DBusSignature left, DBusSignature right) => left.Equals(right);

    public static bool operator !=(DBusSignature left, DBusSignature right) => !left.Equals(right);
}
