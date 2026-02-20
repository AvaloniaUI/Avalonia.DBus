using System;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
interface IDBusDiagnostics
{
    void Log(DBusLogLevel level, string message);
    void OnUnobservedException(Exception exception);
}
