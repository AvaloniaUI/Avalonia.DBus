using Avalonia.DBus;

namespace SourceGenHelloWorld;

internal class ConsoleDiagnostics : IDBusDiagnostics
{
    public void Log(DBusLogLevel level, string message)
    {
        Console.Error.WriteLine($"[{level}] {message}");
    }

    public void OnUnobservedException(Exception exception)
    {
        Console.Error.WriteLine($"[Exception] {exception}");
    }
}