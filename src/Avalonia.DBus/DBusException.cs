using System;

namespace Avalonia.DBus;

/// <summary>
/// Exception thrown when a D-Bus method call returns an ERROR message.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
class DBusException : Exception
{
    /// <summary>
    /// The D-Bus error name (e.g., "org.freedesktop.DBus.Error.ServiceUnknown").
    /// </summary>
    public string ErrorName { get; }

    /// <summary>
    /// The original ERROR message, if available.
    /// </summary>
    public DBusMessage? ErrorReply { get; }

    public DBusException(string errorName, string? message = null)
        : base(string.IsNullOrEmpty(message) ? errorName : $"{errorName}: {message}")
    {
        if (string.IsNullOrEmpty(errorName))
        {
            throw new ArgumentException("Error name is required.", nameof(errorName));
        }

        ErrorName = errorName;
        ErrorReply = null;
    }

    public DBusException(string errorName, string? message, DBusMessage dbusMessage)
        : base(string.IsNullOrEmpty(message) ? errorName : $"{errorName}: {message}")
    {
        if (string.IsNullOrEmpty(errorName))
        {
            throw new ArgumentException("Error name is required.", nameof(errorName));
        }

        ErrorName = errorName;
        ErrorReply = dbusMessage ?? throw new ArgumentNullException(nameof(dbusMessage));
    }
}
