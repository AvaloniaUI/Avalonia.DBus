using System;

namespace Avalonia.DBus;

internal readonly struct DBusSignatureToken(char value) : IEquatable<DBusSignatureToken>
{
    public static readonly DBusSignatureToken Invalid = new('\0');
    public static readonly DBusSignatureToken Byte = new('y');
    public static readonly DBusSignatureToken Boolean = new('b');
    public static readonly DBusSignatureToken Int16 = new('n');
    public static readonly DBusSignatureToken UInt16 = new('q');
    public static readonly DBusSignatureToken Int32 = new('i');
    public static readonly DBusSignatureToken UInt32 = new('u');
    public static readonly DBusSignatureToken Int64 = new('x');
    public static readonly DBusSignatureToken UInt64 = new('t');
    public static readonly DBusSignatureToken Double = new('d');
    public static readonly DBusSignatureToken String = new('s');
    public static readonly DBusSignatureToken ObjectPath = new('o');
    public static readonly DBusSignatureToken Signature = new('g');
    public static readonly DBusSignatureToken UnixFd = new('h');
    public static readonly DBusSignatureToken Array = new('a');
    public static readonly DBusSignatureToken StructBegin = new('(');
    public static readonly DBusSignatureToken StructEnd = new(')');
    public static readonly DBusSignatureToken Variant = new('v');
    public static readonly DBusSignatureToken DictEntryBegin = new('{');
    public static readonly DBusSignatureToken DictEntryEnd = new('}');

    public char Value { get; } = value;

    public static implicit operator DBusSignatureToken(char value) => new(value);

    public static implicit operator DBusSignatureToken(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 1)
            throw new ArgumentException("Signature token string must be a single character.", nameof(value));

        return new DBusSignatureToken(value[0]);
    }

    public static implicit operator char(DBusSignatureToken token) => token.Value;

    public static implicit operator string(DBusSignatureToken token) => token.Value.ToString();

    public bool Equals(DBusSignatureToken other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is DBusSignatureToken other && Equals(other);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value.ToString();

    public static bool operator ==(DBusSignatureToken left, DBusSignatureToken right) => left.Equals(right);

    public static bool operator !=(DBusSignatureToken left, DBusSignatureToken right) => !left.Equals(right);
}
