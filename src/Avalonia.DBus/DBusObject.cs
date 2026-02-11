using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Avalonia.DBus;

public sealed class DBusObject : ICollection<IDBusInterfaceHandler>, IDBusObject
{
    private const string ErrorUnknownInterface = "org.freedesktop.DBus.Error.UnknownInterface";

    private readonly object _gate = new();
    private readonly List<IDBusInterfaceHandler> _interfaces = [];
    private readonly List<DBusObject> _children = [];

    private DBusObject? _parent;

    public DBusObject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        Path = new DBusObjectPath(path);
    }

    public DBusObject(DBusObjectPath path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must be provided.", nameof(path));

        Path = path;
    }

    public DBusObjectPath Path { get; }

    public DBusObject? Parent
    {
        get
        {
            lock (_gate)
            {
                return _parent;
            }
        }
    }

    public IReadOnlyList<DBusObject> Children
    {
        get
        {
            lock (_gate)
            {
                return _children.ToArray();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal event EventHandler<DBusObjectChildChangedEventArgs>? ChildAdded;
    internal event EventHandler<DBusObjectChildChangedEventArgs>? ChildRemoved;

    public bool TryGetIntrospectionXml(string iface, out XmlDocument value)
    {
        var handler = FindInterface(iface);
        if (handler == null)
        {
            value = null!;
            return false;
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(handler.IntrospectXml);
            value = doc;
            return true;
        }
        catch
        {
            value = null!;
            return false;
        }
    }

    public bool TryGetProperty(string iface, string name, out DBusVariant value)
    {
        var handler = FindInterface(iface);
        if (handler != null)
            return handler.TryGetProperty(name, out value);

        value = new DBusVariant(string.Empty);
        return false;
    }

    public bool TrySetProperty(string iface, string name, DBusVariant value)
    {
        var handler = FindInterface(iface);
        return handler != null && handler.TrySetProperty(name, value);
    }

    public bool TryGetAllProperties(string iface, out Dictionary<string, DBusVariant> props)
    {
        var handler = FindInterface(iface);
        if (handler == null)
        {
            props = new Dictionary<string, DBusVariant>(StringComparer.Ordinal);
            return false;
        }

        props = handler.GetAllProperties();
        return true;
    }

    public bool TryGetInterfaces(out IReadOnlyList<string> ifaces)
    {
        lock (_gate)
        {
            ifaces = _interfaces
                .Where(static handler => !string.IsNullOrEmpty(handler.InterfaceName))
                .Select(static handler => handler.InterfaceName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static iface => iface, StringComparer.Ordinal)
                .ToArray();
            return true;
        }
    }

    public Task<DBusMessage> InvokeMember(DBusMessage request)
    {
        var iface = request.Interface;
        if (string.IsNullOrEmpty(iface))
            return Task.FromResult(request.CreateError(ErrorUnknownInterface, "Unknown interface"));

        var handler = FindInterface(iface);
        if (handler == null)
            return Task.FromResult(request.CreateError(ErrorUnknownInterface, "Unknown interface"));

        return handler.HandleMethodAsync(request);
    }

    public void AddChild(DBusObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        lock (_gate)
        {
            if (ReferenceEquals(child, this))
                throw new InvalidOperationException("An object cannot be a child of itself.");

            for (var current = _parent; current != null; current = current._parent)
            {
                if (ReferenceEquals(current, child))
                    throw new InvalidOperationException("Adding this child would create a cycle.");
            }

            if (child._parent != null && !ReferenceEquals(child._parent, this))
                throw new InvalidOperationException("Child already has a parent.");

            if (_children.Contains(child))
                return;

            child._parent = this;
            _children.Add(child);
        }

        ChildAdded?.Invoke(this, new DBusObjectChildChangedEventArgs(child));
    }

    public bool RemoveChild(DBusObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        lock (_gate)
        {
            if (!_children.Remove(child))
                return false;

            child._parent = null;
        }

        ChildRemoved?.Invoke(this, new DBusObjectChildChangedEventArgs(child));
        return true;
    }

    public void Add(IDBusInterfaceHandler item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_gate)
        {
            item.Path = Path;
            _interfaces.Add(item);
        }
    }

    public bool Contains(IDBusInterfaceHandler item)
    {
        lock (_gate)
        {
            return _interfaces.Contains(item);
        }
    }

    public bool Remove(IDBusInterfaceHandler item)
    {
        lock (_gate)
        {
            item.Path = null;
            return _interfaces.Remove(item);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            foreach (var item in _interfaces)
                item.Path = null;

            _interfaces.Clear();
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _interfaces.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public void CopyTo(IDBusInterfaceHandler[] array, int arrayIndex)
    {
        lock (_gate)
        {
            _interfaces.CopyTo(array, arrayIndex);
        }
    }

    public IEnumerator<IDBusInterfaceHandler> GetEnumerator()
    {
        lock (_gate)
        {
            return _interfaces.ToArray().AsEnumerable().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void NotifyPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private IDBusInterfaceHandler? FindInterface(string iface)
    {
        if (string.IsNullOrEmpty(iface))
            return null;

        lock (_gate)
        {
            return _interfaces.FirstOrDefault(handler => string.Equals(handler.InterfaceName, iface, StringComparison.Ordinal));
        }
    }
}

internal sealed class DBusObjectChildChangedEventArgs(DBusObject child) : EventArgs
{
    public DBusObject Child { get; } = child;
}
