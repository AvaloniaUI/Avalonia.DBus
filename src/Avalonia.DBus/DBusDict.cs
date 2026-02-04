using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Avalonia.DBus;

/// <summary>
/// Represents a D-Bus dictionary (array of dict entries).
/// </summary>
public sealed class DBusDict<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IDBusDict
    where TKey : notnull
{
    private readonly Dictionary<TKey, TValue> _items;

    public DBusDict()
    {
        _items = new Dictionary<TKey, TValue>();
    }

    public DBusDict(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        _items = new Dictionary<TKey, TValue>();
        if (items != null)
        {
            foreach (var item in items)
            {
                _items[item.Key] = item.Value;
            }
        }
    }

    public int Count => _items.Count;

    public TValue this[TKey key] => _items[key];

    public IEnumerable<TKey> Keys => _items.Keys;

    public IEnumerable<TValue> Values => _items.Values;

    public bool ContainsKey(TKey key) => _items.ContainsKey(key);

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _items.TryGetValue(key, out value);

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    Type IDBusDict.KeyType => typeof(TKey);

    Type IDBusDict.ValueType => typeof(TValue);

    IEnumerable<KeyValuePair<object?, object?>> IDBusDict.Entries => EnumerateEntries();

    private IEnumerable<KeyValuePair<object?, object?>> EnumerateEntries()
    {
        foreach (var entry in _items)
        {
            yield return new KeyValuePair<object?, object?>(entry.Key, entry.Value);
        }
    }
}
