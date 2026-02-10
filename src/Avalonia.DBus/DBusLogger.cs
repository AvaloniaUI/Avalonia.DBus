using System;

namespace Avalonia.DBus;

public sealed class DBusLogger
{
    /// <summary>
    /// Verbose/debug logging hook.
    /// </summary>
    public Action<string>? Verbose { get; init; }

    /// <summary>
    /// Informational logging hook.
    /// </summary>
    public Action<string>? Info { get; init; }

    /// <summary>
    /// Warning logging hook.
    /// </summary>
    public Action<string>? Warning { get; init; }

    /// <summary>
    /// Error logging hook.
    /// </summary>
    public Action<string>? Error { get; init; }

    /// <summary>
    /// Wire-level verbose/debug logging hook.
    /// </summary>
    public Action<string>? WireVerbose { get; init; }

    /// <summary>
    /// Wire-level informational logging hook.
    /// </summary>
    public Action<string>? WireInfo { get; init; }

    /// <summary>
    /// Wire-level warning logging hook.
    /// </summary>
    public Action<string>? WireWarning { get; init; }

    /// <summary>
    /// Wire-level error logging hook.
    /// </summary>
    public Action<string>? WireError { get; init; }

    internal static DBusLogger CreateDefault()
    {
#if DEBUG
        return new DBusLogger
        {
            Verbose = static message => Console.Error.WriteLine(message),
            WireVerbose = static message => Console.Error.WriteLine(message),
        };
#else
        return new DBusLogger();
#endif
    }
}
