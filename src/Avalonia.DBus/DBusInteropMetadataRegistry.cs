using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
static class DBusInteropMetadataRegistry
{
    private static readonly object Gate = new();

    private static RegistrySnapshot s_snapshot = RegistrySnapshot.Empty;

    public static void Register(DBusInteropMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(metadata.ClrType);

        if (string.IsNullOrWhiteSpace(metadata.InterfaceName))
            throw new InvalidOperationException("Interface name must be provided.");

        lock (Gate)
        {
            var snapshot = s_snapshot;
            var byClrType = snapshot.ByClrType;
            if (byClrType.TryGetValue(metadata.ClrType, out var existing))
            {
                if (!string.Equals(existing.InterfaceName, metadata.InterfaceName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"CLR type '{metadata.ClrType.FullName}' is already registered for D-Bus interface '{existing.InterfaceName}'.");
                }

                var merged = new DBusInteropMetadata
                {
                    ClrType = existing.ClrType,
                    InterfaceName = existing.InterfaceName,
                    CreateProxy = existing.CreateProxy ?? metadata.CreateProxy,
                    CreateHandler = existing.CreateHandler ?? metadata.CreateHandler,
                    TrySetProperty = existing.TrySetProperty ?? metadata.TrySetProperty,
                    GetAllPropertiesFactory = existing.GetAllPropertiesFactory ?? metadata.GetAllPropertiesFactory,
                    WriteIntrospectionXml = existing.WriteIntrospectionXml ?? metadata.WriteIntrospectionXml
                };

                var mergedMap = new Dictionary<Type, DBusInteropMetadata>(byClrType)
                {
                    [metadata.ClrType] = merged
                };

                s_snapshot = new RegistrySnapshot(mergedMap);
                return;
            }

            var updated = new Dictionary<Type, DBusInteropMetadata>(byClrType)
            {
                [metadata.ClrType] = metadata
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

        if (!TryGetByClrType(clrType, out var metadata) || metadata.CreateProxy == null)
        {
            throw new InvalidOperationException(
                $"No generated proxy registration exists for CLR type '{clrType.FullName}'.");
        }

        var resolvedInterface = string.IsNullOrWhiteSpace(iface)
            ? metadata.InterfaceName
            : iface;

        return metadata.CreateProxy(connection, destination, path, resolvedInterface);
    }

    internal static IReadOnlyList<DBusInteropMetadata> ResolveHandlerRegistrations(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var snapshot = s_snapshot;
        List<DBusInteropMetadata>? registrations = null;
        foreach (var metadata in snapshot.ByClrType.Values)
        {
            if (metadata.CreateHandler == null)
                continue;

            if (!metadata.ClrType.IsAssignableFrom(targetType))
                continue;

            registrations ??= [];
            registrations.Add(metadata);
        }

        if (registrations == null)
            return [];

        registrations.Sort(static (left, right) => string.CompareOrdinal(left.InterfaceName, right.InterfaceName));
        return registrations;
    }

    internal static WriteIntrospectionXmlFactory? GetIntrospectionWriter(string interfaceName)
    {
        var snapshot = s_snapshot;
        foreach (var metadata in snapshot.ByClrType.Values)
        {
            if (string.Equals(metadata.InterfaceName, interfaceName, StringComparison.Ordinal)
                && metadata.WriteIntrospectionXml is not null)
                return metadata.WriteIntrospectionXml;
        }
        return null;
    }

    private static bool TryGetByClrType(Type clrType, out DBusInteropMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        var snapshot = s_snapshot;
        return snapshot.ByClrType.TryGetValue(clrType, out metadata!);
    }

    private sealed class RegistrySnapshot(IReadOnlyDictionary<Type, DBusInteropMetadata> byClrType)
    {
        public static RegistrySnapshot Empty { get; } = new(new Dictionary<Type, DBusInteropMetadata>());

        public IReadOnlyDictionary<Type, DBusInteropMetadata> ByClrType { get; } = byClrType;
    }
}
