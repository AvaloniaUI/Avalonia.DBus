using System;

namespace Avalonia.DBus;

public enum DBusLogLevel { Verbose, Info, Warning, Error }

public interface IDBusDiagnostics
{
    void Log(DBusLogLevel level, string message);
    void OnUnobservedException(Exception exception);
}
