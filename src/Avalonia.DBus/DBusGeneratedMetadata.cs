using System;
using System.Collections.Generic;

namespace Avalonia.DBus;

public static class DBusGeneratedMetadata
{
    private static readonly object Gate = new();

    private static RegistrySnapshot s_snapshot = RegistrySnapshot.Empty;

    public static void Register(DBusInterfaceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ValidateDescriptor(descriptor);

        lock (Gate)
        {
            var snapshot = s_snapshot;
            var byName = snapshot.ByInterfaceName;
            var byClrType = snapshot.ByClrInterfaceType;

            var hasName = byName.TryGetValue(descriptor.InterfaceName, out var existingByName);
            var hasClrType = byClrType.TryGetValue(descriptor.ClrInterfaceType, out var existingByClrType);

            if (hasName || hasClrType)
            {
                if (hasName && hasClrType)
                {
                    if (!IsEquivalent(existingByName!, descriptor) || !IsEquivalent(existingByClrType!, descriptor))
                    {
                        throw new InvalidOperationException(
                            $"Conflicting D-Bus descriptor registration for interface '{descriptor.InterfaceName}' and CLR type '{descriptor.ClrInterfaceType.FullName}'.");
                    }

                    if (!IsEquivalent(existingByName!, existingByClrType!))
                    {
                        throw new InvalidOperationException(
                            $"Inconsistent D-Bus descriptor registry state for interface '{descriptor.InterfaceName}' and CLR type '{descriptor.ClrInterfaceType.FullName}'.");
                    }

                    return;
                }

                if (hasName)
                {
                    throw new InvalidOperationException(
                        $"D-Bus interface '{descriptor.InterfaceName}' is already registered with CLR type '{existingByName!.ClrInterfaceType.FullName}'.");
                }

                throw new InvalidOperationException(
                    $"CLR interface type '{descriptor.ClrInterfaceType.FullName}' is already registered for D-Bus interface '{existingByClrType!.InterfaceName}'.");
            }

            var updatedByName = new Dictionary<string, DBusInterfaceDescriptor>(byName, StringComparer.Ordinal)
            {
                [descriptor.InterfaceName] = descriptor
            };

            var updatedByClrType = new Dictionary<Type, DBusInterfaceDescriptor>(byClrType)
            {
                [descriptor.ClrInterfaceType] = descriptor
            };

            s_snapshot = new RegistrySnapshot(updatedByName, updatedByClrType);
        }
    }

    internal static bool TryGetByInterfaceName(string iface, out DBusInterfaceDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(iface))
        {
            descriptor = null!;
            return false;
        }

        var snapshot = s_snapshot;
        return snapshot.ByInterfaceName.TryGetValue(iface, out descriptor!);
    }

    internal static bool TryGetByClrType(Type clrInterfaceType, out DBusInterfaceDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(clrInterfaceType);

        var snapshot = s_snapshot;
        return snapshot.ByClrInterfaceType.TryGetValue(clrInterfaceType, out descriptor!);
    }

    private static void ValidateDescriptor(DBusInterfaceDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.InterfaceName))
            throw new InvalidOperationException("Descriptor InterfaceName must be provided.");

        if (descriptor.ClrInterfaceType == null)
            throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' is missing ClrInterfaceType.");

        if (string.IsNullOrWhiteSpace(descriptor.IntrospectionXml))
            throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' is missing IntrospectionXml.");

        if (descriptor.Dispatcher == null)
            throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' is missing Dispatcher.");

        if (descriptor.Properties == null)
            throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' is missing property metadata.");

        if (descriptor.Methods == null)
            throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' is missing method metadata.");

        foreach (var (name, property) in descriptor.Properties)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' contains a property with an empty key.");

            if (property == null)
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' contains a null property descriptor for key '{name}'.");

            if (!string.Equals(name, property.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Descriptor '{descriptor.InterfaceName}' property key '{name}' does not match descriptor name '{property.Name}'.");
            }

            if (string.IsNullOrWhiteSpace(property.Signature))
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' property '{name}' is missing signature metadata.");

            if (property.CanRead && property.TryGet == null)
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' property '{name}' is readable but TryGet delegate is missing.");

            if (property.CanWrite && property.TrySet == null)
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' property '{name}' is writable but TrySet delegate is missing.");
        }

        foreach (var (name, method) in descriptor.Methods)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' contains a method with an empty key.");

            if (method == null)
                throw new InvalidOperationException($"Descriptor '{descriptor.InterfaceName}' contains a null method descriptor for key '{name}'.");

            if (!string.Equals(name, method.Name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Descriptor '{descriptor.InterfaceName}' method key '{name}' does not match descriptor name '{method.Name}'.");
            }
        }
    }

    private static bool IsEquivalent(DBusInterfaceDescriptor left, DBusInterfaceDescriptor right)
    {
        if (!string.Equals(left.InterfaceName, right.InterfaceName, StringComparison.Ordinal))
            return false;

        if (left.ClrInterfaceType != right.ClrInterfaceType)
            return false;

        if (!string.Equals(left.IntrospectionXml, right.IntrospectionXml, StringComparison.Ordinal))
            return false;

        if (!ReferenceEquals(left.Dispatcher, right.Dispatcher)
            && left.Dispatcher.GetType() != right.Dispatcher.GetType())
        {
            return false;
        }

        if (!ArePropertiesEquivalent(left.Properties, right.Properties))
            return false;

        return AreMethodsEquivalent(left.Methods, right.Methods);
    }

    private static bool ArePropertiesEquivalent(
        IReadOnlyDictionary<string, DBusPropertyDescriptor> left,
        IReadOnlyDictionary<string, DBusPropertyDescriptor> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var (name, leftProperty) in left)
        {
            if (!right.TryGetValue(name, out var rightProperty))
                return false;

            if (!string.Equals(leftProperty.Name, rightProperty.Name, StringComparison.Ordinal)
                || leftProperty.CanRead != rightProperty.CanRead
                || leftProperty.CanWrite != rightProperty.CanWrite
                || !string.Equals(leftProperty.Signature, rightProperty.Signature, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreMethodsEquivalent(
        IReadOnlyDictionary<string, DBusMethodDescriptor> left,
        IReadOnlyDictionary<string, DBusMethodDescriptor> right)
    {
        if (left.Count != right.Count)
            return false;

        foreach (var (name, leftMethod) in left)
        {
            if (!right.TryGetValue(name, out var rightMethod))
                return false;

            if (!string.Equals(leftMethod.Name, rightMethod.Name, StringComparison.Ordinal)
                || !string.Equals(leftMethod.InSignature, rightMethod.InSignature, StringComparison.Ordinal)
                || !string.Equals(leftMethod.OutSignature, rightMethod.OutSignature, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private sealed class RegistrySnapshot(
        IReadOnlyDictionary<string, DBusInterfaceDescriptor> byInterfaceName,
        IReadOnlyDictionary<Type, DBusInterfaceDescriptor> byClrInterfaceType)
    {
        public static RegistrySnapshot Empty { get; } = new(
            new Dictionary<string, DBusInterfaceDescriptor>(StringComparer.Ordinal),
            new Dictionary<Type, DBusInterfaceDescriptor>());

        public IReadOnlyDictionary<string, DBusInterfaceDescriptor> ByInterfaceName { get; } = byInterfaceName;

        public IReadOnlyDictionary<Type, DBusInterfaceDescriptor> ByClrInterfaceType { get; } = byClrInterfaceType;
    }
}
