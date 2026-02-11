using System;
using System.Collections.Generic;
using System.Threading;

namespace Avalonia.DBus;

public sealed class DBusExportedTargetBindingBuilder
{
    private readonly List<DBusBoundInterface> _bindings = [];

    public void Bind<TInterface>(
        TInterface target,
        SynchronizationContext? synchronizationContext = null)
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(target);

        if (!DBusGeneratedMetadata.TryGetByClrType(typeof(TInterface), out var descriptor))
        {
            throw new InvalidOperationException(
                $"No generated D-Bus descriptor is registered for CLR interface '{typeof(TInterface).FullName}'. " +
                "Ensure source-generated module initializers have run before binding exports.");
        }

        _bindings.Add(new DBusBoundInterface(descriptor, target, synchronizationContext));
    }

    internal IReadOnlyList<DBusBoundInterface> Snapshot()
    {
        return _bindings.Count == 0 ? throw new InvalidOperationException("At least one bound interface is required.") : _bindings.ToArray();
    }
}