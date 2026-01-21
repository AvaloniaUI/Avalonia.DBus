using System;

namespace Avalonia.DBus.Wire;

public sealed class DBusException : Exception
{
    public DBusException(string name, string message)
        : base(string.IsNullOrEmpty(message) ? name : $"{name}: {message}")
    {
        Name = name;
    }

    public string Name { get; }
}
