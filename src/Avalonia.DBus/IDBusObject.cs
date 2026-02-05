using System.Collections.Generic;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public interface IDBusObject
{
    DBusObjectPath Path { get; }

    string IntrospectXml { get; }

    bool HasInterface(string name);

    bool TryGetProperty(string iface, string name, out DBusVariant value);

    bool TrySetProperty(string iface, string name, DBusVariant value);

    Dictionary<string, DBusVariant> GetAllProperties(string iface);

    Task<DBusMessage> HandleMethodAsync(DBusMessage request);
}
