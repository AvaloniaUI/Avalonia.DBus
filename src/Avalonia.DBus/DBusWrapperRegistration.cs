using System;

namespace Avalonia.DBus;

internal sealed class DBusWrapperRegistration
{
    public required Type ClrType { get; init; }

    public required string InterfaceName { get; init; }

    public required Func<IDBusConnection, string, DBusObjectPath, string, object> CreateProxy { get; init; }
}