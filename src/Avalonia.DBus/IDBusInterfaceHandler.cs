using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace Avalonia.DBus;

public interface IDBusInterfaceHandler
{
    DBusConnection Connection { get; }

    DBusObjectPath? Path { get; set; }

    string InterfaceName { get; }

    XmlDocument IntrospectXml { get; }

    Task<DBusMessage> HandleMethodAsync(DBusMessage request);

    bool TryGetProperty(string name, out DBusVariant value);

    bool TrySetProperty(string name, DBusVariant value);

    Dictionary<string, DBusVariant> GetAllProperties();
}
