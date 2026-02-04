using System;
using System.Collections;
using System.Collections.Generic;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus array. Generic type parameter enables signature inference.
/// </summary>
public sealed class DBusArray<T> : IReadOnlyList<T>, IDBusArray
{
    private readonly List<T> _items;
    private readonly string? _elementSignature;

    public DBusArray()
    {
        _items = [];
    }

    public DBusArray(string elementSignature)
    {
        _elementSignature = string.IsNullOrEmpty(elementSignature) ? null : elementSignature;
        _items = [];
    }

    public DBusArray(IEnumerable<T> items)
    {
        _items = items == null ? [] : [..items];
    }

    public DBusArray(string elementSignature, IEnumerable<T> items)
    {
        _elementSignature = string.IsNullOrEmpty(elementSignature) ? null : elementSignature;
        _items = items == null ? [] : [..items];
    }

    public DBusArray(params T[] items)
    {
        _items = items == null ? [] : [..items];
    }

    public DBusArray(string elementSignature, params T[] items)
    {
        _elementSignature = string.IsNullOrEmpty(elementSignature) ? null : elementSignature;
        _items = items == null ? [] : [..items];
    }

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    Type IDBusArray.ElementType => typeof(T);

    string? IDBusArray.ElementSignature => _elementSignature;

    IEnumerable<object?> IDBusArray.Items => EnumerateObjects();

    private IEnumerable<object?> EnumerateObjects()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }
}
