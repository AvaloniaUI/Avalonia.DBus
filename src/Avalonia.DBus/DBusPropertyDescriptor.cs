namespace Avalonia.DBus;

public sealed class DBusPropertyDescriptor
{
    public required string Name { get; init; }

    public required bool CanRead { get; init; }

    public required bool CanWrite { get; init; }

    public required string Signature { get; init; }

    public DBusPropertyTryGetDelegate? TryGet { get; init; }

    public DBusPropertyTrySetDelegate? TrySet { get; init; }
}