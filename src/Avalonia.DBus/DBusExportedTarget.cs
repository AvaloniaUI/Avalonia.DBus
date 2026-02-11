using System;
using System.Collections.Generic;
using System.Threading;

namespace Avalonia.DBus;

public sealed class DBusExportedTarget
{
    private DBusExportedTarget(object target, IReadOnlyList<DBusBoundInterface> boundInterfaces)
    {
        _target = target;
        _boundInterfaces = boundInterfaces;
    }

    private readonly object _target;

    private readonly IReadOnlyList<DBusBoundInterface> _boundInterfaces;

    internal object Target => _target;

    internal IReadOnlyList<DBusBoundInterface> BoundInterfaces => _boundInterfaces;

    public static DBusExportedTarget Create(
        object target,
        Action<DBusExportedTargetBindingBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new DBusExportedTargetBindingBuilder();
        configure(builder);
        var bindings = builder.Snapshot();

        return new DBusExportedTarget(target, bindings);
    }

    public static DBusExportedTarget Create<TInterface>(
        TInterface target,
        SynchronizationContext? synchronizationContext = null)
        where TInterface : class
    {
        ArgumentNullException.ThrowIfNull(target);
        return Create(target, builder => builder.Bind(target, synchronizationContext));
    }
}
