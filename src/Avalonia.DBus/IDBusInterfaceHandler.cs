using System.Collections.Generic;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public interface IDBusInterfaceHandler
{
    DBusConnection Connection { get; }

    DBusObjectPath? Path { get; set; }

    string InterfaceName { get; }

    string IntrospectXml { get; }

    Task<DBusMessage> HandleMethodAsync(DBusMessage request);

    bool TryGetProperty(string name, out DBusVariant value);

    bool TrySetProperty(string name, DBusVariant value);

    Dictionary<string, DBusVariant> GetAllProperties();
}
