using System;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus variant (dynamically typed value).
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
sealed class DBusVariant
{
    /// <summary>
    /// The D-Bus type signature of the contained value.
    /// </summary>
    public DBusSignature Signature { get; }

    /// <summary>
    /// The contained value.
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Creates a variant with an inferred signature based on the value's .NET type.
    /// </summary>
    public DBusVariant(object value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Signature = new DBusSignature(DBusSignatureInference.InferSignatureFromValue(value));
    }

    /// <summary>
    /// Creates a variant with an explicit D-Bus signature, bypassing inference.
    /// Use when the signature is known at compile time to avoid inference failures on empty arrays.
    /// </summary>
    public DBusVariant(object value, string signature)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Signature = new DBusSignature(signature ?? throw new ArgumentNullException(nameof(signature)));
    }
}
