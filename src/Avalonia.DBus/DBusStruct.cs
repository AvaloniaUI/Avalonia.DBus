using System;
using System.Collections;
using System.Collections.Generic;

namespace Avalonia.DBus.Wire;

/// <summary>
/// Represents a D-Bus struct (sequence of typed fields).
/// </summary>
public sealed class DBusStruct : IReadOnlyList<object>
{
    private readonly List<object> _fields;

    public DBusStruct(params object[] fields)
        : this((IEnumerable<object>)fields ?? Array.Empty<object>())
    {
    }

    public DBusStruct(IEnumerable<object> fields)
    {
        _fields = fields == null ? new List<object>() : new List<object>(fields);
    }

    public int Count => _fields.Count;

    public object this[int index] => _fields[index];

    public IEnumerator<object> GetEnumerator() => _fields.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _fields.GetEnumerator();
}
