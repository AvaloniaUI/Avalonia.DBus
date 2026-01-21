namespace Avalonia.DBus.Wire;

public readonly struct VariantValue
{
    public VariantValue(string signature, object? value)
    {
        Signature = signature ?? string.Empty;
        Value = value;
    }

    public string Signature { get; }
    public object? Value { get; }

    public override string ToString() => $"{Signature}:{Value}";

    public static VariantValue FromSignature(string signature, object? value) => new VariantValue(signature, value);

    public static implicit operator VariantValue(byte value) => new VariantValue("y", value);
    public static implicit operator VariantValue(bool value) => new VariantValue("b", value);
    public static implicit operator VariantValue(short value) => new VariantValue("n", value);
    public static implicit operator VariantValue(ushort value) => new VariantValue("q", value);
    public static implicit operator VariantValue(int value) => new VariantValue("i", value);
    public static implicit operator VariantValue(uint value) => new VariantValue("u", value);
    public static implicit operator VariantValue(long value) => new VariantValue("x", value);
    public static implicit operator VariantValue(ulong value) => new VariantValue("t", value);
    public static implicit operator VariantValue(double value) => new VariantValue("d", value);
    public static implicit operator VariantValue(string value) => new VariantValue("s", value);
    public static implicit operator VariantValue(ObjectPath value) => new VariantValue("o", value.ToString());
    public static implicit operator VariantValue(Signature value) => new VariantValue("g", value.ToString());
}
