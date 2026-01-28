using System;
using System.Collections.Generic;

namespace Avalonia.DBus.Wire;

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
            case IDBusArray dbusArray:
                return string.Concat(DBusSignatureToken.Array, InferArrayElementSignature(dbusArray));
            case IDBusDict dbusDict:
                return string.Concat(
                    DBusSignatureToken.Array,
                    DBusSignatureToken.DictEntryBegin,
                    InferDictKeySignature(dbusDict),
                    InferDictValueSignature(dbusDict),
                    DBusSignatureToken.DictEntryEnd);
            default:
                throw new NotSupportedException($"Unsupported D-Bus value type: {value.GetType().FullName}");
        }
    }

    internal static string InferSignatureFromType(Type type)
    {
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
        if (type.IsGenericType)
        {
            var genericType = type.GetGenericTypeDefinition();
            if (genericType == typeof(DBusArray<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return string.Concat(DBusSignatureToken.Array, InferSignatureFromType(elementType));
            }
            if (genericType == typeof(DBusDict<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                return string.Concat(
                    DBusSignatureToken.Array,
                    DBusSignatureToken.DictEntryBegin,
                    InferSignatureFromType(keyType),
                    InferSignatureFromType(valueType),
                    DBusSignatureToken.DictEntryEnd);
            }
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
            int index = 1;
            string elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
            if (elementSignature.Length > 0 && elementSignature[0] == DBusSignatureToken.DictEntryBegin)
            {
                var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(elementSignature);
                Type keyType = GetTypeForSignature(keySig);
                Type valueType = GetTypeForSignature(valueSig);
                return typeof(DBusDict<,>).MakeGenericType(keyType, valueType);
            }

            Type elementType = GetTypeForSignature(elementSignature);
            return typeof(DBusArray<>).MakeGenericType(elementType);
        }
        if (token == DBusSignatureToken.StructBegin)
        {
            return typeof(DBusStruct);
        }
        if (token == DBusSignatureToken.DictEntryBegin)
        {
            var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
            Type keyType = GetTypeForSignature(keySig);
            Type valueType = GetTypeForSignature(valueSig);
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

    private static string InferArrayElementSignature(IDBusArray array)
    {
        if (!string.IsNullOrEmpty(array.ElementSignature))
        {
            return array.ElementSignature!;
        }

        try
        {
            return InferSignatureFromType(array.ElementType);
        }
        catch (NotSupportedException)
        {
            foreach (var item in array.Items)
            {
                if (item == null)
                {
                    throw new InvalidOperationException("Array contains null values; cannot infer element signature.");
                }
                return InferSignatureFromValue(item);
            }

            throw new InvalidOperationException("Array is empty; cannot infer element signature for this type.");
        }
    }

    private static string InferDictKeySignature(IDBusDict dict)
    {
        try
        {
            return InferSignatureFromType(dict.KeyType);
        }
        catch (NotSupportedException)
        {
            foreach (var entry in dict.Entries)
            {
                if (entry.Key == null)
                {
                    throw new InvalidOperationException("Dictionary contains null keys; cannot infer signature.");
                }
                return InferSignatureFromValue(entry.Key);
            }

            throw new InvalidOperationException("Dictionary is empty; cannot infer key signature for this type.");
        }
    }

    private static string InferDictValueSignature(IDBusDict dict)
    {
        try
        {
            return InferSignatureFromType(dict.ValueType);
        }
        catch (NotSupportedException)
        {
            foreach (var entry in dict.Entries)
            {
                if (entry.Value == null)
                {
                    throw new InvalidOperationException("Dictionary contains null values; cannot infer signature.");
                }
                return InferSignatureFromValue(entry.Value);
            }

            throw new InvalidOperationException("Dictionary is empty; cannot infer value signature for this type.");
        }
    }
}
