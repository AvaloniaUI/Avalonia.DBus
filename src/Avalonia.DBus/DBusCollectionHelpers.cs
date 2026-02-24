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

        if (TryGetGenericType(type, typeof(List<>), out var args)
            || TryGetGenericType(type, typeof(IList<>), out args)
            || TryGetGenericType(type, typeof(IReadOnlyList<>), out args))
        {
            elementType = args[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    internal static bool TryGetDictionaryTypes(Type type, out Type keyType, out Type valueType)
    {
        if (TryGetGenericType(type, typeof(Dictionary<,>), out var args)
            || TryGetGenericType(type, typeof(IDictionary<,>), out args)
            || TryGetGenericType(type, typeof(IReadOnlyDictionary<,>), out args))
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
        if (list is not IEnumerable enumerable)
            yield break;

        foreach (var item in enumerable)
            yield return item;
    }

    internal static IEnumerable<KeyValuePair<object?, object?>> EnumerateDictionaryEntries(object dictionary)
    {
        if (dictionary is IDictionary nonGeneric)
        {
            foreach (DictionaryEntry entry in nonGeneric)
                yield return new KeyValuePair<object?, object?>(entry.Key, entry.Value);
            yield break;
        }

        if (dictionary is IEnumerable enumerable)
        {
            var sawSupportedEntries = false;
            var sawUnsupportedEntries = false;

            foreach (var entry in enumerable)
            {
                if (entry is DictionaryEntry dictionaryEntry)
                {
                    sawSupportedEntries = true;
                    yield return new KeyValuePair<object?, object?>(dictionaryEntry.Key, dictionaryEntry.Value);
                }
                else if (entry != null)
                {
                    sawUnsupportedEntries = true;
                }
            }

            if (sawUnsupportedEntries && !sawSupportedEntries)
            {
                throw new NotSupportedException(
                    $"Dictionary enumeration for type '{dictionary.GetType().FullName}' requires IDictionary support.");
            }
        }
    }

    private static bool TryGetGenericType(Type type, Type openGeneric, out Type[] args)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGeneric)
            {
                args = current.GetGenericArguments();
                return true;
            }
        }

        args = [];
        return false;
    }
}
