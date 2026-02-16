using System.Diagnostics;
using Avalonia.DBus;

namespace Atspi2TestApp;

internal class DBusConsoleDiagnostics : IDBusDiagnostics
{
    private static readonly Stopwatch s_uptime = Stopwatch.StartNew();

    public void Log(DBusLogLevel level, string message)
    {
        Console.Error.WriteLine($@"[{s_uptime.Elapsed:hh\:mm\:ss\.fff}] [{level}] {message}");
    }

    public void OnUnobservedException(Exception exception)
    {
        Console.Error.WriteLine($@"[{s_uptime.Elapsed:hh\:mm\:ss\.fff}] [Exception] {exception}");
    }
}