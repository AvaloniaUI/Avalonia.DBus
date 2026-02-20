namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
static class DBusConnectionProxyExtensions
{
    public static T CreateProxy<T>(
        this IDBusConnection connection,
        string destination,
        DBusObjectPath path,
        string? iface = null)
        where T : class
    {
        return (T)connection.CreateProxy(typeof(T), destination, path, iface);
    }
}
