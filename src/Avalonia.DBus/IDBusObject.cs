using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Xml;

namespace Avalonia.DBus;

public interface IDBusObject : INotifyPropertyChanged
{
    DBusObjectPath Path { get; }

    bool TryGetIntrospectionXml(string iface, out XmlDocument value);

    bool TryGetProperty(string iface, string name, out DBusVariant value);

    bool TrySetProperty(string iface, string name, DBusVariant value);

    bool TryGetAllProperties(string iface, out Dictionary<string, DBusVariant> props);

    bool TryGetInterfaces(out IReadOnlyList<string> ifaces);

    Task<DBusMessage> InvokeMember(DBusMessage request);
}
