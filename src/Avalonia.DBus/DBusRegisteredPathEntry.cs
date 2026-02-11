using System;
using System.Collections.Generic;
using System.Threading;

namespace Avalonia.DBus;

internal sealed class DBusRegisteredPathEntry(
    long registrationId,
    string path,
    DBusExportedTarget exportedTarget,
    IReadOnlyDictionary<string, DBusBoundInterfaceRegistration> interfacesByName,
    SynchronizationContext? defaultSynchronizationContext)
{
    public long RegistrationId { get; } = registrationId;

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    public DBusExportedTarget ExportedTarget { get; } = exportedTarget ?? throw new ArgumentNullException(nameof(exportedTarget));

    public IReadOnlyDictionary<string, DBusBoundInterfaceRegistration> InterfacesByName { get; } =
        interfacesByName ?? throw new ArgumentNullException(nameof(interfacesByName));

    public SynchronizationContext? DefaultSynchronizationContext { get; } = defaultSynchronizationContext;

    public bool TryGetBinding(string iface, out DBusBoundInterfaceRegistration binding)
    {
        if (string.IsNullOrEmpty(iface))
        {
            binding = null!;
            return false;
        }

        return InterfacesByName.TryGetValue(iface, out binding!);
    }
}