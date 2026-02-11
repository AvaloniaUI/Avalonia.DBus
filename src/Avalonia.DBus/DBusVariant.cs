using System;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus variant (dynamically typed value).
/// </summary>
public sealed class DBusVariant
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
}
