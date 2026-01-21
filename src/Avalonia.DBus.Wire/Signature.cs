namespace Avalonia.DBus.Wire;

public readonly struct Signature
{
    public Signature(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator Signature(string value) => new Signature(value);

    public static implicit operator string(Signature value) => value.Value;
}
