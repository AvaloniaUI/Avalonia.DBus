using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Avalonia.DBus;

internal static class DBusSignatureInference
{
    internal static string InferBodySignature(IReadOnlyList<object> body)
    {
        if (body == null || body.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(body.Count);
        foreach (var item in body)
        {
            parts.Add(InferSignatureFromValue(item));
        }

        return string.Concat(parts);
    }

    internal static string InferSignatureFromValue(object value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        switch (value)
        {
            case byte:
                return DBusSignatureToken.Byte;
            case bool:
                return DBusSignatureToken.Boolean;
            case short:
                return DBusSignatureToken.Int16;
            case ushort:
                return DBusSignatureToken.UInt16;
            case int:
                return DBusSignatureToken.Int32;
            case uint:
                return DBusSignatureToken.UInt32;
            case long:
                return DBusSignatureToken.Int64;
            case ulong:
                return DBusSignatureToken.UInt64;
            case double:
                return DBusSignatureToken.Double;
            case string:
                return DBusSignatureToken.String;
            case DBusObjectPath:
                return DBusSignatureToken.ObjectPath;
            case DBusSignature:
                return DBusSignatureToken.Signature;
            case DBusUnixFd:
                return DBusSignatureToken.UnixFd;
            case DBusVariant:
                return DBusSignatureToken.Variant;
            case DBusStruct dbusStruct:
                return InferStructSignature(dbusStruct);
            default:
                return InferSignatureFromCollectionOrStruct(value);
        }
    }

    private static string InferSignatureFromCollectionOrStruct(object value)
    {
        var type = value.GetType();
        if (TryGetStructSignatureFromType(type, out var structSignature))
        {
            return structSignature;
        }

        if (DBusCollectionHelpers.TryGetDictionaryTypes(type, out var keyType, out var valueType))
        {
            return string.Concat(
                DBusSignatureToken.Array,
                DBusSignatureToken.DictEntryBegin,
                InferDictKeySignature(value, keyType),
                InferDictValueSignature(value, valueType),
                DBusSignatureToken.DictEntryEnd);
        }

        if (DBusCollectionHelpers.TryGetListElementType(type, out var elementType))
        {
            return string.Concat(DBusSignatureToken.Array, InferArrayElementSignature(value, elementType));
        }

        if (value is IDictionary)
        {
            return string.Concat(
                DBusSignatureToken.Array,
                DBusSignatureToken.DictEntryBegin,
                InferDictKeySignatureFromEntries(value),
                InferDictValueSignatureFromEntries(value),
                DBusSignatureToken.DictEntryEnd);
        }

        if (value is IList)
        {
            return string.Concat(DBusSignatureToken.Array, InferArrayElementSignatureFromItems(value));
        }

        throw new NotSupportedException($"Unsupported D-Bus value type: {type.FullName}");
    }

    internal static string InferSignatureFromType(Type type)
    {
        if (TryGetStructSignatureFromType(type, out var structSignature))
        {
            return structSignature;
        }
        if (type == typeof(byte))
        {
            return DBusSignatureToken.Byte;
        }
        if (type == typeof(bool))
        {
            return DBusSignatureToken.Boolean;
        }
        if (type == typeof(short))
        {
            return DBusSignatureToken.Int16;
        }
        if (type == typeof(ushort))
        {
            return DBusSignatureToken.UInt16;
        }
        if (type == typeof(int))
        {
            return DBusSignatureToken.Int32;
        }
        if (type == typeof(uint))
        {
            return DBusSignatureToken.UInt32;
        }
        if (type == typeof(long))
        {
            return DBusSignatureToken.Int64;
        }
        if (type == typeof(ulong))
        {
            return DBusSignatureToken.UInt64;
        }
        if (type == typeof(double))
        {
            return DBusSignatureToken.Double;
        }
        if (type == typeof(string))
        {
            return DBusSignatureToken.String;
        }
        if (type == typeof(DBusObjectPath))
        {
            return DBusSignatureToken.ObjectPath;
        }
        if (type == typeof(DBusSignature))
        {
            return DBusSignatureToken.Signature;
        }
        if (type == typeof(DBusUnixFd))
        {
            return DBusSignatureToken.UnixFd;
        }
        if (type == typeof(DBusVariant))
        {
            return DBusSignatureToken.Variant;
        }
        if (type == typeof(DBusStruct))
        {
            throw new NotSupportedException("DBusStruct requires value-based signature inference.");
        }
        if (DBusCollectionHelpers.TryGetDictionaryTypes(type, out var keyType, out var valueType))
        {
            return string.Concat(
                DBusSignatureToken.Array,
                DBusSignatureToken.DictEntryBegin,
                InferSignatureFromType(keyType),
                InferSignatureFromType(valueType),
                DBusSignatureToken.DictEntryEnd);
        }
        if (DBusCollectionHelpers.TryGetListElementType(type, out var elementType))
        {
            return string.Concat(DBusSignatureToken.Array, InferSignatureFromType(elementType));
        }

        throw new NotSupportedException($"Unsupported D-Bus type: {type.FullName}");
    }

    internal static Type GetTypeForSignature(string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            throw new ArgumentException("Signature is required.", nameof(signature));
        }

        DBusSignatureToken token = signature[0];
        if (token == DBusSignatureToken.Byte)
        {
            return typeof(byte);
        }
        if (token == DBusSignatureToken.Boolean)
        {
            return typeof(bool);
        }
        if (token == DBusSignatureToken.Int16)
        {
            return typeof(short);
        }
        if (token == DBusSignatureToken.UInt16)
        {
            return typeof(ushort);
        }
        if (token == DBusSignatureToken.Int32)
        {
            return typeof(int);
        }
        if (token == DBusSignatureToken.UInt32)
        {
            return typeof(uint);
        }
        if (token == DBusSignatureToken.Int64)
        {
            return typeof(long);
        }
        if (token == DBusSignatureToken.UInt64)
        {
            return typeof(ulong);
        }
        if (token == DBusSignatureToken.Double)
        {
            return typeof(double);
        }
        if (token == DBusSignatureToken.String)
        {
            return typeof(string);
        }
        if (token == DBusSignatureToken.ObjectPath)
        {
            return typeof(DBusObjectPath);
        }
        if (token == DBusSignatureToken.Signature)
        {
            return typeof(DBusSignature);
        }
        if (token == DBusSignatureToken.UnixFd)
        {
            return typeof(DBusUnixFd);
        }
        if (token == DBusSignatureToken.Variant)
        {
            return typeof(DBusVariant);
        }
        if (token == DBusSignatureToken.Array)
        {
            var index = 1;
            var elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
            if (elementSignature.Length > 0 && elementSignature[0] == DBusSignatureToken.DictEntryBegin)
            {
                var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(elementSignature);
                var keyType = GetTypeForSignature(keySig);
                var valueType = GetTypeForSignature(valueSig);
                return typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            }

            var elementType = GetTypeForSignature(elementSignature);
            return typeof(List<>).MakeGenericType(elementType);
        }
        if (token == DBusSignatureToken.StructBegin)
        {
            return typeof(DBusStruct);
        }
        if (token == DBusSignatureToken.DictEntryBegin)
        {
            var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
            var keyType = GetTypeForSignature(keySig);
            var valueType = GetTypeForSignature(valueSig);
            return typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
        }

        throw new NotSupportedException($"Unsupported D-Bus signature token: {signature[0]}");
    }

    private static string InferStructSignature(DBusStruct dbusStruct)
    {
        var parts = new List<string>(dbusStruct.Count);
        foreach (var field in dbusStruct)
        {
            parts.Add(InferSignatureFromValue(field));
        }

        return string.Concat(DBusSignatureToken.StructBegin, string.Concat(parts), DBusSignatureToken.StructEnd);
    }

    private static string InferArrayElementSignature(object array, Type elementType)
    {
        if (RequiresValueBasedInference(elementType))
        {
            return InferArrayElementSignatureFromItems(array);
        }

        try
        {
            return InferSignatureFromType(elementType);
        }
        catch (NotSupportedException)
        {
            return InferArrayElementSignatureFromItems(array);
        }
    }

    private static string InferDictKeySignature(object dict, Type keyType)
    {
        if (RequiresValueBasedInference(keyType))
        {
            return InferDictKeySignatureFromEntries(dict);
        }

        try
        {
            return InferSignatureFromType(keyType);
        }
        catch (NotSupportedException)
        {
            return InferDictKeySignatureFromEntries(dict);
        }
    }

    private static string InferDictValueSignature(object dict, Type valueType)
    {
        if (RequiresValueBasedInference(valueType))
        {
            return InferDictValueSignatureFromEntries(dict);
        }

        try
        {
            return InferSignatureFromType(valueType);
        }
        catch (NotSupportedException)
        {
            return InferDictValueSignatureFromEntries(dict);
        }
    }

    private static bool RequiresValueBasedInference(Type type)
        => type == typeof(DBusStruct);

    private static string InferArrayElementSignatureFromItems(object array)
    {
        foreach (var item in DBusCollectionHelpers.EnumerateListItems(array))
        {
            if (item == null)
            {
                throw new InvalidOperationException("Array contains null values; cannot infer element signature.");
            }
            return InferSignatureFromValue(item);
        }

        throw new InvalidOperationException("Array is empty; cannot infer element signature for this type.");
    }

    private static string InferDictKeySignatureFromEntries(object dict)
    {
        foreach (var entry in DBusCollectionHelpers.EnumerateDictionaryEntries(dict))
        {
            if (entry.Key == null)
            {
                throw new InvalidOperationException("Dictionary contains null keys; cannot infer signature.");
            }
            return InferSignatureFromValue(entry.Key);
        }

        throw new InvalidOperationException("Dictionary is empty; cannot infer key signature for this type.");
    }

    private static string InferDictValueSignatureFromEntries(object dict)
    {
        foreach (var entry in DBusCollectionHelpers.EnumerateDictionaryEntries(dict))
        {
            if (entry.Value == null)
            {
                throw new InvalidOperationException("Dictionary contains null values; cannot infer signature.");
            }
            return InferSignatureFromValue(entry.Value);
        }

        throw new InvalidOperationException("Dictionary is empty; cannot infer value signature for this type.");
    }

    private static bool TryGetStructSignatureFromType(Type type, out string signature)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

        var field = type.GetField("Signature", flags);
        if (field is not null && field.FieldType == typeof(string))
        {
            var value = field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null);
            if (value is string literal)
            {
                signature = literal;
                return true;
            }
        }

        var property = type.GetProperty("Signature", flags);
        if (property is not null && property.PropertyType == typeof(string) && property.GetMethod is not null)
        {
            if (property.GetValue(null) is string literal)
            {
                signature = literal;
                return true;
            }
        }

        signature = string.Empty;
        return false;
    }
}
