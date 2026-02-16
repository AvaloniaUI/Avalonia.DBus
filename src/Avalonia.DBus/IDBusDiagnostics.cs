using System;

namespace Avalonia.DBus;

public interface IDBusDiagnostics
{
    void Log(DBusLogLevel level, string message);
    void OnUnobservedException(Exception exception);
}
