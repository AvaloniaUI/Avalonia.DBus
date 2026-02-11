using System;

namespace Avalonia.DBus;

public readonly struct DBusSubtreeRegistration
{
    public DBusSubtreeRegistration(
        string fullPath,
        DBusExportedTarget target,
        IDBusSubtreeLifecycle? lifecycle = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Path must be provided.", nameof(fullPath));

        FullPath = fullPath;
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Lifecycle = lifecycle;
    }

    public string FullPath { get; }

    public DBusExportedTarget Target { get; }

    public IDBusSubtreeLifecycle? Lifecycle { get; }
}