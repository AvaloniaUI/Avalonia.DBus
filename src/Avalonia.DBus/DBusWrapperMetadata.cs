using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

public static class DBusWrapperMetadata
{
    private static readonly object Gate = new();

    private static RegistrySnapshot s_snapshot = RegistrySnapshot.Empty;

    public static void Register(
        Type clrType,
        string interfaceName,
        Func<IDBusConnection, string, DBusObjectPath, string, object> createProxy)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(createProxy);

        if (string.IsNullOrWhiteSpace(interfaceName))
            throw new InvalidOperationException("Interface name must be provided.");

        lock (Gate)
        {
            var snapshot = s_snapshot;
            var byClrType = snapshot.ByClrType;
            if (byClrType.TryGetValue(clrType, out var existing))
            {
                if (!string.Equals(existing.InterfaceName, interfaceName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CLR type '{clrType.FullName}' is already registered for D-Bus interface '{existing.InterfaceName}'.");
                }

                return;
            }

            var updated = new Dictionary<Type, DBusWrapperRegistration>(byClrType)
            {
                [clrType] = new DBusWrapperRegistration
                {
                    ClrType = clrType,
                    InterfaceName = interfaceName,
                    CreateProxy = createProxy
                }
            };

            s_snapshot = new RegistrySnapshot(updated);
        }
    }

    public static object CreateProxy(
        Type clrType,
        IDBusConnection connection,
        string destination,
        DBusObjectPath path,
        string? iface = null)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(connection);

        if (string.IsNullOrWhiteSpace(destination))
            throw new ArgumentException("Destination is required.", nameof(destination));

        if (!TryGetByClrType(clrType, out var registration))
        {
            throw new InvalidOperationException(
                $"No generated proxy registration exists for CLR type '{clrType.FullName}'.");
        }

        var resolvedInterface = string.IsNullOrWhiteSpace(iface)
            ? registration.InterfaceName
            : iface;

        return registration.CreateProxy(connection, destination, path, resolvedInterface);
    }

    private static bool TryGetByClrType(Type clrType, out DBusWrapperRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var snapshot = s_snapshot;
        return snapshot.ByClrType.TryGetValue(clrType, out registration!);
    }

    private sealed class RegistrySnapshot(IReadOnlyDictionary<Type, DBusWrapperRegistration> byClrType)
    {
        public static RegistrySnapshot Empty { get; } =
            new(new Dictionary<Type, DBusWrapperRegistration>());

        public IReadOnlyDictionary<Type, DBusWrapperRegistration> ByClrType { get; } = byClrType;
    }
}
