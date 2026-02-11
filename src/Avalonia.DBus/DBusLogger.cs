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

    internal static DBusLogger CreateDefault()
    {
#if DEBUG
        return new DBusLogger
        {
            Verbose = static message => Console.Error.WriteLine(message),
            Info = static message => Console.Error.WriteLine(message),
            Warning = static message => Console.Error.WriteLine(message),
            Error = static message => Console.Error.WriteLine(message),
        };
#else
        return new DBusLogger();
#endif
    }
}