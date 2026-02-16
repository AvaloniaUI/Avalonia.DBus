namespace Avalonia.DBus;

public delegate object CreateProxyFactory(
    IDBusConnection connection,
    string destination,
    DBusObjectPath path,
    string iface);