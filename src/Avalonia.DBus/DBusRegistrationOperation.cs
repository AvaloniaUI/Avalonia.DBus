using System;

namespace Avalonia.DBus;

public readonly struct DBusRegistrationOperation
{
    internal RegistrationOperationKind Kind { get; }

    public string Path { get; }

    public DBusExportedTarget? Target { get; }

    private DBusRegistrationOperation(RegistrationOperationKind kind, string path, DBusExportedTarget? target)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        if (kind is RegistrationOperationKind.Add or RegistrationOperationKind.Replace)
            ArgumentNullException.ThrowIfNull(target);

        Kind = kind;
        Path = path;
        Target = target;
    }

    public static DBusRegistrationOperation Add(string fullPath, DBusExportedTarget target)
        => new(RegistrationOperationKind.Add, fullPath, target);

    public static DBusRegistrationOperation Remove(string fullPath)
        => new(RegistrationOperationKind.Remove, fullPath, target: null);

    public static DBusRegistrationOperation Replace(string fullPath, DBusExportedTarget target)
        => new(RegistrationOperationKind.Replace, fullPath, target);
}