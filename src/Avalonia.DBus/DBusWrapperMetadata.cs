using System;
using System.Collections.Generic;
using System.Threading;

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

                if (existing.CreateProxy != null)
                    return;

                var merged = new DBusWrapperRegistration
                {
                    ClrType = existing.ClrType,
                    InterfaceName = existing.InterfaceName,
                    CreateProxy = createProxy,
                    RegisterObject = existing.RegisterObject,
                    TryGetProperty = existing.TryGetProperty,
                    TrySetProperty = existing.TrySetProperty,
                    GetAllProperties = existing.GetAllProperties
                };

                var mergedMap = new Dictionary<Type, DBusWrapperRegistration>(byClrType)
                {
                    [clrType] = merged
                };

                s_snapshot = new RegistrySnapshot(mergedMap);
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

    public static void RegisterService(
        Type clrType,
        string interfaceName,
        Func<IDBusConnection, DBusObjectPath, object, SynchronizationContext?, IDisposable> registerObject,
        Func<object, string, DBusVariant?>? tryGetProperty = null,
        Func<object, string, DBusVariant, bool>? trySetProperty = null,
        Func<object, IReadOnlyDictionary<string, DBusVariant>>? getAllProperties = null)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        ArgumentNullException.ThrowIfNull(registerObject);

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

                if (existing.RegisterObject != null)
                    return;

                var merged = new DBusWrapperRegistration
                {
                    ClrType = existing.ClrType,
                    InterfaceName = existing.InterfaceName,
                    CreateProxy = existing.CreateProxy,
                    RegisterObject = registerObject,
                    TryGetProperty = tryGetProperty,
                    TrySetProperty = trySetProperty,
                    GetAllProperties = getAllProperties
                };

                var mergedMap = new Dictionary<Type, DBusWrapperRegistration>(byClrType)
                {
                    [clrType] = merged
                };

                s_snapshot = new RegistrySnapshot(mergedMap);
                return;
            }

            var updated = new Dictionary<Type, DBusWrapperRegistration>(byClrType)
            {
                [clrType] = new DBusWrapperRegistration
                {
                    ClrType = clrType,
                    InterfaceName = interfaceName,
                    RegisterObject = registerObject,
                    TryGetProperty = tryGetProperty,
                    TrySetProperty = trySetProperty,
                    GetAllProperties = getAllProperties
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

        if (!TryGetByClrType(clrType, out var registration) || registration.CreateProxy == null)
        {
            throw new InvalidOperationException(
                $"No generated proxy registration exists for CLR type '{clrType.FullName}'.");
        }

        var resolvedInterface = string.IsNullOrWhiteSpace(iface)
            ? registration.InterfaceName
            : iface;

        return registration.CreateProxy(connection, destination, path, resolvedInterface);
    }

    internal static IReadOnlyList<DBusWrapperRegistration> ResolveServiceRegistrations(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        var snapshot = s_snapshot;
        List<DBusWrapperRegistration>? registrations = null;
        foreach (var registration in snapshot.ByClrType.Values)
        {
            if (registration.RegisterObject == null)
                continue;

            if (!registration.ClrType.IsAssignableFrom(targetType))
                continue;

            registrations ??= [];
            registrations.Add(registration);
        }

        if (registrations == null)
            return Array.Empty<DBusWrapperRegistration>();

        registrations.Sort(static (left, right) => string.CompareOrdinal(left.InterfaceName, right.InterfaceName));
        return registrations;
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


 
 internal sealed class DBusWrapperRegistration
 {
   public required Type ClrType { get; init; }
 
   public required string InterfaceName { get; init; }
 
   public Func<IDBusConnection, string, DBusObjectPath, string, object>? CreateProxy { get; init; }
 
   public Func<IDBusConnection, DBusObjectPath, object, SynchronizationContext?, IDisposable>? RegisterObject { get; init; }
 
  public Func<object, string, DBusVariant?>? TryGetProperty { get; init; }
 
   public Func<object, string, DBusVariant, bool>? TrySetProperty { get; init; }
    
        public Func<object, IReadOnlyDictionary<string, DBusVariant>>? GetAllProperties { get; init; }
     }