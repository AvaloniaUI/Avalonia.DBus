using System;
using System.Collections;
using System.Collections.Generic;

namespace Avalonia.DBus;

internal static class DBusCollectionHelpers
{
    internal static bool TryGetListElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (TryGetGenericInterface(type, typeof(IList<>), out var args)
            || TryGetGenericInterface(type, typeof(IReadOnlyList<>), out args))
        {
            elementType = args[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    internal static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        if (TryGetGenericInterface(type, typeof(IDictionary<,>), out var args)
            || TryGetGenericInterface(type, typeof(IReadOnlyDictionary<,>), out args))
        {
            keyType = args[0];
            valueType = args[1];
            return true;
        }

        keyType = null!;
        valueType = null!;
        return false;
    }

    internal static IEnumerable<object?> EnumerateListItems(object list)
    {
        if (list is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }

    internal static IEnumerable<KeyValuePair<object?, object?>> EnumerateDictionaryEntries(object dictionary)
    {
        if (dictionary is IDictionary nonGeneric)
        {
            foreach (DictionaryEntry entry in nonGeneric)
            {
                yield return new KeyValuePair<object?, object?>(entry.Key, entry.Value);
            }

            yield break;
        }

        if (dictionary is IEnumerable enumerable)
        {
            foreach (var entry in enumerable)
            {
                if (entry is null)
                {
                    continue;
                }

                var entryType = entry.GetType();
                if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    var key = entryType.GetProperty("Key")?.GetValue(entry);
                    var value = entryType.GetProperty("Value")?.GetValue(entry);
                    yield return new KeyValuePair<object?, object?>(key, value);
                }
            }
        }
    }

    private static bool TryGetGenericInterface(Type type, Type openGeneric, out Type[] args)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == openGeneric)
        {
            args = type.GetGenericArguments();
            return true;
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == openGeneric)
            {
                args = iface.GetGenericArguments();
                return true;
            }
        }

        args = [];
        return false;
    }
}
