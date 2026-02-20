using System.Collections;
using System.Collections.Generic;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus struct (sequence of typed fields).
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
sealed class DBusStruct(IEnumerable<object> fields) : IReadOnlyList<object>
{
    private readonly List<object> _fields = fields == null ? [] : [.. fields];

    public DBusStruct(params object[] fields)
        : this((IEnumerable<object>)fields ?? [])
    {
    }

    public int Count => _fields.Count;

    public object this[int index] => _fields[index];

    public IEnumerator<object> GetEnumerator() => _fields.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _fields.GetEnumerator();
}
