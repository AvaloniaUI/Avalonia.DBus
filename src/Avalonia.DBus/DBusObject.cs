using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public sealed class DBusObject : ICollection<IDBusInterfaceHandler>, IDBusObject
{
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";
    private static readonly DBusVariant MissingProperty = new(string.Empty);

    private readonly List<IDBusInterfaceHandler> _interfaces = [];
    private string[] _childNodes = [];

    public DBusObject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path must be provided.", nameof(path));
        }

        Path = new DBusObjectPath(path);
    }

    public DBusObject(DBusObjectPath path)
    {
        Path = path;
    }

    public DBusObjectPath Path { get; }

    public string IntrospectXml => BuildIntrospectionXml();

    public bool HasInterface(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return FindInterface(name) != null;
    }

    public bool TryGetProperty(string iface, string name, out DBusVariant value)
    {
        var handler = FindInterface(iface);
        if (handler != null) return handler.TryGetProperty(name, out value);
        value = MissingProperty;
        return false;
    }

    public bool TrySetProperty(string iface, string name, DBusVariant value)
    {
        var handler = FindInterface(iface);
        return handler != null && handler.TrySetProperty(name, value);
    }

    public Dictionary<string, DBusVariant> GetAllProperties(string iface)
    {
        var handler = FindInterface(iface);
        if (handler == null)
        {
            throw new InvalidOperationException("Unknown interface.");
        }

        return handler.GetAllProperties();
    }

    public Task<DBusMessage> HandleMethodAsync(DBusMessage request)
    {
        var iface = request.Interface;
        if (string.IsNullOrEmpty(iface))
        {
            return Task.FromResult(request.CreateError(ErrorUnknownInterface, "Unknown interface"));
        }

        var handler = FindInterface(iface);
        return handler == null ? Task.FromResult(request.CreateError(ErrorUnknownInterface, "Unknown interface"))
            : handler.HandleMethodAsync(request);
    }

    public void Add(IDBusInterfaceHandler item)
    {
        item.Path = Path;
        _interfaces.Add(item);
    }

    public bool Contains(IDBusInterfaceHandler item) => _interfaces.Contains(item);

    public bool Remove(IDBusInterfaceHandler item)
    {
        item.Path = null;
        var removed = _interfaces.Remove(item);
        return removed;
    }

    public void Clear()
    {
        foreach (var item in _interfaces) item.Path = null;

        _interfaces.Clear();
    }

    public int Count => _interfaces.Count;

    public bool IsReadOnly => false;

    public void CopyTo(IDBusInterfaceHandler[] array, int arrayIndex) => _interfaces.CopyTo(array, arrayIndex);

    public IEnumerator<IDBusInterfaceHandler> GetEnumerator() => _interfaces.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _interfaces.GetEnumerator();

    internal void SetChildNodes(string[] childNodes)
    {
        _childNodes = childNodes ?? [];
    }

    private IDBusInterfaceHandler? FindInterface(string iface)
    {
        return string.IsNullOrEmpty(iface) ? null
            : _interfaces.FirstOrDefault(handler =>
                string.Equals(handler.InterfaceName, iface, StringComparison.Ordinal));
    }

    private string BuildIntrospectionXml()
    {
        var sb = new StringBuilder();
        sb.Append("<node>");

        foreach (var handler in _interfaces)
            sb.Append(handler.IntrospectXml);

        if (_interfaces.Count > 0 && DBusBuiltIns.EnablePropertiesInterface)
            sb.Append(DBusBuiltIns.PropertiesIntrospectXml);

        if (DBusBuiltIns.EnableIntrospectableInterface)
            sb.Append(DBusBuiltIns.IntrospectableIntrospectXml);

        if (DBusBuiltIns.EnablePeerInterface) 
            sb.Append(DBusBuiltIns.PeerIntrospectXml);

        foreach (var child in _childNodes)
        {
            sb.Append("<node name='");
            sb.Append(child);
            sb.Append("'/>");
        }

        sb.Append("</node>");
        return sb.ToString();
    }
}
