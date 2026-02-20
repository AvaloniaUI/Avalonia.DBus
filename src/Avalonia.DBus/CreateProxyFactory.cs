namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
delegate object CreateProxyFactory(
    IDBusConnection connection,
    string destination,
    DBusObjectPath path,
    string iface);
