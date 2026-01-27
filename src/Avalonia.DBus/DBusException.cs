using System;

namespace Avalonia.DBus.Wire;

/// <summary>
/// Exception thrown when a D-Bus method call returns an ERROR message.
/// </summary>
public class DBusException : Exception
{
    /// <summary>
    /// The D-Bus error name (e.g., "org.freedesktop.DBus.Error.ServiceUnknown").
    /// </summary>
    public string ErrorName { get; }

    /// <summary>
    /// The original ERROR message.
    /// </summary>
    public new DBusMessage Message { get; }

    public DBusException(string errorName, string? message, DBusMessage dbusMessage)
        : base(string.IsNullOrEmpty(message) ? errorName : $"{errorName}: {message}")
    {
        if (string.IsNullOrEmpty(errorName))
        {
            throw new ArgumentException("Error name is required.", nameof(errorName));
        }

        ErrorName = errorName;
        Message = dbusMessage ?? throw new ArgumentNullException(nameof(dbusMessage));
    }
}
