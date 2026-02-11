using System;
using System.Threading;

namespace Avalonia.DBus;

internal sealed class DBusBoundInterface(
    DBusInterfaceDescriptor descriptor,
    object target,
    SynchronizationContext? synchronizationContext = null)
{
    public DBusInterfaceDescriptor Descriptor { get; } = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

    public object Target { get; } = target ?? throw new ArgumentNullException(nameof(target));

    public SynchronizationContext? SynchronizationContext { get; } = synchronizationContext;
}