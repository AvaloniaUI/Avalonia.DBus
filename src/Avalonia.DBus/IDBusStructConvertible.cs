namespace Avalonia.DBus;

/// <summary>
/// Represents a value that can be converted to a D-Bus struct payload.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
interface IDBusStructConvertible
{
    DBusStruct ToDbusStruct();
}
