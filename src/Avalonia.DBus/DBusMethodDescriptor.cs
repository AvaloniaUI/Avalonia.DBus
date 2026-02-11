namespace Avalonia.DBus;

public sealed class DBusMethodDescriptor
{
    public required string Name { get; init; }

    public required string InSignature { get; init; }

    public required string OutSignature { get; init; }
}