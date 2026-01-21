namespace Avalonia.DBus.Wire;

public readonly struct ObjectPath
{
    public ObjectPath(string value)
    {
        Value = value ?? string.Empty;
    }

    public string Value { get; }

    public override string ToString() => Value;

    public static implicit operator ObjectPath(string value) => new ObjectPath(value);

    public static implicit operator string(ObjectPath value) => value.Value;
}
