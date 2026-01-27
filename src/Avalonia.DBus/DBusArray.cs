using System;
using System.Collections;
using System.Collections.Generic;

namespace Avalonia.DBus.Wire;

internal interface IDBusArray
{
    Type ElementType { get; }

    IEnumerable<object?> Items { get; }
}

/// <summary>
/// Represents a D-Bus array. Generic type parameter enables signature inference.
/// </summary>
public sealed class DBusArray<T> : IReadOnlyList<T>, IDBusArray
{
    private readonly List<T> _items;

    public DBusArray()
    {
        _items = new List<T>();
    }

    public DBusArray(IEnumerable<T> items)
    {
        _items = items == null ? new List<T>() : new List<T>(items);
    }

    public DBusArray(params T[] items)
    {
        _items = items == null ? new List<T>() : new List<T>(items);
    }

    public int Count => _items.Count;

    public T this[int index] => _items[index];

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    Type IDBusArray.ElementType => typeof(T);

    IEnumerable<object?> IDBusArray.Items => EnumerateObjects();

    private IEnumerable<object?> EnumerateObjects()
    {
        foreach (var item in _items)
        {
            yield return item;
        }
    }
}
