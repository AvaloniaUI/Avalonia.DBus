using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Avalonia.DBus;

internal static class DBusSignatureInference
{
    internal static string InferBodySignature(IReadOnlyList<object>? body)
    {
        if (body == null || body.Count == 0)
            return string.Empty;

        var parts = new List<string>(body.Count);
        parts.AddRange(body.Select(InferSignatureFromValue));

        return string.Concat(parts);
    }

    internal static string InferSignatureFromValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

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
            case IDBusStructConvertible structConvertible:
                return InferStructSignature(structConvertible.ToDbusStruct());
            default:
                return InferSignatureFromCollectionOrStruct(value);
        }
    }

    private static string InferSignatureFromCollectionOrStruct(object value)
    {
        var type = value.GetType();

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
        if (typeof(IDBusStructConvertible).IsAssignableFrom(type))
        {
            if (DBusStructSignatureRegistry.TryGetSignature(type, out var sig) 
                && !string.IsNullOrEmpty(sig))
                return sig;
            throw new NotSupportedException($"{type.FullName} requires value-based signature inference.");
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
            throw new ArgumentException("Signature is required.", nameof(signature));

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
                return GetDictionaryTypeForSignature(keySig, valueSig);
            }

            return GetListTypeForSignature(elementSignature);
        }
        if (token == DBusSignatureToken.StructBegin)
        {
            return typeof(DBusStruct);
        }
        if (token == DBusSignatureToken.DictEntryBegin)
        {
            var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
            return GetKeyValuePairTypeForSignature(keySig, valueSig);
        }

        throw new NotSupportedException($"Unsupported D-Bus signature token: {signature[0]}");
    }

    private static Type GetListTypeForSignature(string elementSignature)
    {
        DBusSignatureToken token = elementSignature[0];
        return token switch
        {
            _ when token == DBusSignatureToken.Byte => typeof(List<byte>),
            _ when token == DBusSignatureToken.Boolean => typeof(List<bool>),
            _ when token == DBusSignatureToken.Int16 => typeof(List<short>),
            _ when token == DBusSignatureToken.UInt16 => typeof(List<ushort>),
            _ when token == DBusSignatureToken.Int32 => typeof(List<int>),
            _ when token == DBusSignatureToken.UInt32 => typeof(List<uint>),
            _ when token == DBusSignatureToken.Int64 => typeof(List<long>),
            _ when token == DBusSignatureToken.UInt64 => typeof(List<ulong>),
            _ when token == DBusSignatureToken.Double => typeof(List<double>),
            _ when token == DBusSignatureToken.String => typeof(List<string>),
            _ when token == DBusSignatureToken.ObjectPath => typeof(List<DBusObjectPath>),
            _ when token == DBusSignatureToken.Signature => typeof(List<DBusSignature>),
            _ when token == DBusSignatureToken.UnixFd => typeof(List<DBusUnixFd>),
            _ when token == DBusSignatureToken.Variant => typeof(List<DBusVariant>),
            _ when token == DBusSignatureToken.StructBegin => typeof(List<DBusStruct>),
            _ when token == DBusSignatureToken.Array => GetNestedListTypeForSignature(elementSignature),
            _ when token == DBusSignatureToken.DictEntryBegin => GetDictionaryListTypeForSignature(elementSignature),
            _ => typeof(List<object>)
        };
    }

    private static Type GetNestedListTypeForSignature(string arraySignature)
    {
        var index = 1;
        var nestedElement = DBusSignatureParser.ReadSingleType(arraySignature, ref index);
        DBusSignatureToken token = nestedElement[0];
        return token switch
        {
            _ when token == DBusSignatureToken.Byte => typeof(List<List<byte>>),
            _ when token == DBusSignatureToken.Boolean => typeof(List<List<bool>>),
            _ when token == DBusSignatureToken.Int16 => typeof(List<List<short>>),
            _ when token == DBusSignatureToken.UInt16 => typeof(List<List<ushort>>),
            _ when token == DBusSignatureToken.Int32 => typeof(List<List<int>>),
            _ when token == DBusSignatureToken.UInt32 => typeof(List<List<uint>>),
            _ when token == DBusSignatureToken.Int64 => typeof(List<List<long>>),
            _ when token == DBusSignatureToken.UInt64 => typeof(List<List<ulong>>),
            _ when token == DBusSignatureToken.Double => typeof(List<List<double>>),
            _ when token == DBusSignatureToken.String => typeof(List<List<string>>),
            _ when token == DBusSignatureToken.ObjectPath => typeof(List<List<DBusObjectPath>>),
            _ when token == DBusSignatureToken.Signature => typeof(List<List<DBusSignature>>),
            _ when token == DBusSignatureToken.UnixFd => typeof(List<List<DBusUnixFd>>),
            _ when token == DBusSignatureToken.Variant => typeof(List<List<DBusVariant>>),
            _ when token == DBusSignatureToken.StructBegin => typeof(List<List<DBusStruct>>),
            _ => typeof(List<object>)
        };
    }

    private static Type GetDictionaryListTypeForSignature(string entrySignature)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        return keySig[0] switch
        {
            _ when keySig[0] == DBusSignatureToken.Byte => GetDictionaryListTypeForValue<byte>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Boolean => GetDictionaryListTypeForValue<bool>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Int16 => GetDictionaryListTypeForValue<short>(valueSig),
            _ when keySig[0] == DBusSignatureToken.UInt16 => GetDictionaryListTypeForValue<ushort>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Int32 => GetDictionaryListTypeForValue<int>(valueSig),
            _ when keySig[0] == DBusSignatureToken.UInt32 => GetDictionaryListTypeForValue<uint>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Int64 => GetDictionaryListTypeForValue<long>(valueSig),
            _ when keySig[0] == DBusSignatureToken.UInt64 => GetDictionaryListTypeForValue<ulong>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Double => GetDictionaryListTypeForValue<double>(valueSig),
            _ when keySig[0] == DBusSignatureToken.String => GetDictionaryListTypeForValue<string>(valueSig),
            _ when keySig[0] == DBusSignatureToken.ObjectPath => GetDictionaryListTypeForValue<DBusObjectPath>(valueSig),
            _ when keySig[0] == DBusSignatureToken.Signature => GetDictionaryListTypeForValue<DBusSignature>(valueSig),
            _ when keySig[0] == DBusSignatureToken.UnixFd => GetDictionaryListTypeForValue<DBusUnixFd>(valueSig),
            _ => typeof(List<object>)
        };
    }

    private static Type GetDictionaryListTypeForValue<TKey>(string valueSignature) where TKey : notnull
    {
        DBusSignatureToken token = valueSignature[0];
        return token switch
        {
            _ when token == DBusSignatureToken.Byte => typeof(List<Dictionary<TKey, byte>>),
            _ when token == DBusSignatureToken.Boolean => typeof(List<Dictionary<TKey, bool>>),
            _ when token == DBusSignatureToken.Int16 => typeof(List<Dictionary<TKey, short>>),
            _ when token == DBusSignatureToken.UInt16 => typeof(List<Dictionary<TKey, ushort>>),
            _ when token == DBusSignatureToken.Int32 => typeof(List<Dictionary<TKey, int>>),
            _ when token == DBusSignatureToken.UInt32 => typeof(List<Dictionary<TKey, uint>>),
            _ when token == DBusSignatureToken.Int64 => typeof(List<Dictionary<TKey, long>>),
            _ when token == DBusSignatureToken.UInt64 => typeof(List<Dictionary<TKey, ulong>>),
            _ when token == DBusSignatureToken.Double => typeof(List<Dictionary<TKey, double>>),
            _ when token == DBusSignatureToken.String => typeof(List<Dictionary<TKey, string>>),
            _ when token == DBusSignatureToken.ObjectPath => typeof(List<Dictionary<TKey, DBusObjectPath>>),
            _ when token == DBusSignatureToken.Signature => typeof(List<Dictionary<TKey, DBusSignature>>),
            _ when token == DBusSignatureToken.UnixFd => typeof(List<Dictionary<TKey, DBusUnixFd>>),
            _ when token == DBusSignatureToken.Variant => typeof(List<Dictionary<TKey, DBusVariant>>),
            _ when token == DBusSignatureToken.StructBegin => typeof(List<Dictionary<TKey, DBusStruct>>),
            _ => typeof(List<object>)
        };
    }

    private static Type GetDictionaryTypeForSignature(string keySignature, string valueSignature)
    {
        return keySignature[0] switch
        {
            _ when keySignature[0] == DBusSignatureToken.Byte => GetDictionaryTypeForValue<byte>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Boolean => GetDictionaryTypeForValue<bool>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int16 => GetDictionaryTypeForValue<short>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt16 => GetDictionaryTypeForValue<ushort>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int32 => GetDictionaryTypeForValue<int>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt32 => GetDictionaryTypeForValue<uint>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int64 => GetDictionaryTypeForValue<long>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt64 => GetDictionaryTypeForValue<ulong>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Double => GetDictionaryTypeForValue<double>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.String => GetDictionaryTypeForValue<string>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.ObjectPath => GetDictionaryTypeForValue<DBusObjectPath>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Signature => GetDictionaryTypeForValue<DBusSignature>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UnixFd => GetDictionaryTypeForValue<DBusUnixFd>(valueSignature),
            _ => typeof(Dictionary<object, object>)
        };
    }

    private static Type GetDictionaryTypeForValue<TKey>(string valueSignature) where TKey : notnull
    {
        DBusSignatureToken token = valueSignature[0];
        if (token == DBusSignatureToken.Array)
        {
            var index = 1;
            var elementSignature = DBusSignatureParser.ReadSingleType(valueSignature, ref index);
            return elementSignature[0] switch
            {
                _ when elementSignature[0] == DBusSignatureToken.Byte => typeof(Dictionary<TKey, List<byte>>),
                _ when elementSignature[0] == DBusSignatureToken.Boolean => typeof(Dictionary<TKey, List<bool>>),
                _ when elementSignature[0] == DBusSignatureToken.Int16 => typeof(Dictionary<TKey, List<short>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt16 => typeof(Dictionary<TKey, List<ushort>>),
                _ when elementSignature[0] == DBusSignatureToken.Int32 => typeof(Dictionary<TKey, List<int>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt32 => typeof(Dictionary<TKey, List<uint>>),
                _ when elementSignature[0] == DBusSignatureToken.Int64 => typeof(Dictionary<TKey, List<long>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt64 => typeof(Dictionary<TKey, List<ulong>>),
                _ when elementSignature[0] == DBusSignatureToken.Double => typeof(Dictionary<TKey, List<double>>),
                _ when elementSignature[0] == DBusSignatureToken.String => typeof(Dictionary<TKey, List<string>>),
                _ when elementSignature[0] == DBusSignatureToken.ObjectPath => typeof(Dictionary<TKey, List<DBusObjectPath>>),
                _ when elementSignature[0] == DBusSignatureToken.Signature => typeof(Dictionary<TKey, List<DBusSignature>>),
                _ when elementSignature[0] == DBusSignatureToken.UnixFd => typeof(Dictionary<TKey, List<DBusUnixFd>>),
                _ when elementSignature[0] == DBusSignatureToken.Variant => typeof(Dictionary<TKey, List<DBusVariant>>),
                _ when elementSignature[0] == DBusSignatureToken.StructBegin => typeof(Dictionary<TKey, List<DBusStruct>>),
                _ => typeof(Dictionary<TKey, object>)
            };
        }

        return token switch
        {
            _ when token == DBusSignatureToken.Byte => typeof(Dictionary<TKey, byte>),
            _ when token == DBusSignatureToken.Boolean => typeof(Dictionary<TKey, bool>),
            _ when token == DBusSignatureToken.Int16 => typeof(Dictionary<TKey, short>),
            _ when token == DBusSignatureToken.UInt16 => typeof(Dictionary<TKey, ushort>),
            _ when token == DBusSignatureToken.Int32 => typeof(Dictionary<TKey, int>),
            _ when token == DBusSignatureToken.UInt32 => typeof(Dictionary<TKey, uint>),
            _ when token == DBusSignatureToken.Int64 => typeof(Dictionary<TKey, long>),
            _ when token == DBusSignatureToken.UInt64 => typeof(Dictionary<TKey, ulong>),
            _ when token == DBusSignatureToken.Double => typeof(Dictionary<TKey, double>),
            _ when token == DBusSignatureToken.String => typeof(Dictionary<TKey, string>),
            _ when token == DBusSignatureToken.ObjectPath => typeof(Dictionary<TKey, DBusObjectPath>),
            _ when token == DBusSignatureToken.Signature => typeof(Dictionary<TKey, DBusSignature>),
            _ when token == DBusSignatureToken.UnixFd => typeof(Dictionary<TKey, DBusUnixFd>),
            _ when token == DBusSignatureToken.Variant => typeof(Dictionary<TKey, DBusVariant>),
            _ when token == DBusSignatureToken.StructBegin => typeof(Dictionary<TKey, DBusStruct>),
            _ => typeof(Dictionary<TKey, object>)
        };
    }

    private static Type GetKeyValuePairTypeForSignature(string keySignature, string valueSignature)
    {
        return keySignature[0] switch
        {
            _ when keySignature[0] == DBusSignatureToken.Byte => GetKeyValuePairTypeForValue<byte>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Boolean => GetKeyValuePairTypeForValue<bool>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int16 => GetKeyValuePairTypeForValue<short>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt16 => GetKeyValuePairTypeForValue<ushort>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int32 => GetKeyValuePairTypeForValue<int>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt32 => GetKeyValuePairTypeForValue<uint>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Int64 => GetKeyValuePairTypeForValue<long>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UInt64 => GetKeyValuePairTypeForValue<ulong>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Double => GetKeyValuePairTypeForValue<double>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.String => GetKeyValuePairTypeForValue<string>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.ObjectPath => GetKeyValuePairTypeForValue<DBusObjectPath>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.Signature => GetKeyValuePairTypeForValue<DBusSignature>(valueSignature),
            _ when keySignature[0] == DBusSignatureToken.UnixFd => GetKeyValuePairTypeForValue<DBusUnixFd>(valueSignature),
            _ => typeof(KeyValuePair<object, object>)
        };
    }

    private static Type GetKeyValuePairTypeForValue<TKey>(string valueSignature) where TKey : notnull
    {
        DBusSignatureToken token = valueSignature[0];
        if (token == DBusSignatureToken.Array)
        {
            var index = 1;
            var elementSignature = DBusSignatureParser.ReadSingleType(valueSignature, ref index);
            return elementSignature[0] switch
            {
                _ when elementSignature[0] == DBusSignatureToken.Byte => typeof(KeyValuePair<TKey, List<byte>>),
                _ when elementSignature[0] == DBusSignatureToken.Boolean => typeof(KeyValuePair<TKey, List<bool>>),
                _ when elementSignature[0] == DBusSignatureToken.Int16 => typeof(KeyValuePair<TKey, List<short>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt16 => typeof(KeyValuePair<TKey, List<ushort>>),
                _ when elementSignature[0] == DBusSignatureToken.Int32 => typeof(KeyValuePair<TKey, List<int>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt32 => typeof(KeyValuePair<TKey, List<uint>>),
                _ when elementSignature[0] == DBusSignatureToken.Int64 => typeof(KeyValuePair<TKey, List<long>>),
                _ when elementSignature[0] == DBusSignatureToken.UInt64 => typeof(KeyValuePair<TKey, List<ulong>>),
                _ when elementSignature[0] == DBusSignatureToken.Double => typeof(KeyValuePair<TKey, List<double>>),
                _ when elementSignature[0] == DBusSignatureToken.String => typeof(KeyValuePair<TKey, List<string>>),
                _ when elementSignature[0] == DBusSignatureToken.ObjectPath => typeof(KeyValuePair<TKey, List<DBusObjectPath>>),
                _ when elementSignature[0] == DBusSignatureToken.Signature => typeof(KeyValuePair<TKey, List<DBusSignature>>),
                _ when elementSignature[0] == DBusSignatureToken.UnixFd => typeof(KeyValuePair<TKey, List<DBusUnixFd>>),
                _ when elementSignature[0] == DBusSignatureToken.Variant => typeof(KeyValuePair<TKey, List<DBusVariant>>),
                _ when elementSignature[0] == DBusSignatureToken.StructBegin => typeof(KeyValuePair<TKey, List<DBusStruct>>),
                _ => typeof(KeyValuePair<TKey, object>)
            };
        }

        return token switch
        {
            _ when token == DBusSignatureToken.Byte => typeof(KeyValuePair<TKey, byte>),
            _ when token == DBusSignatureToken.Boolean => typeof(KeyValuePair<TKey, bool>),
            _ when token == DBusSignatureToken.Int16 => typeof(KeyValuePair<TKey, short>),
            _ when token == DBusSignatureToken.UInt16 => typeof(KeyValuePair<TKey, ushort>),
            _ when token == DBusSignatureToken.Int32 => typeof(KeyValuePair<TKey, int>),
            _ when token == DBusSignatureToken.UInt32 => typeof(KeyValuePair<TKey, uint>),
            _ when token == DBusSignatureToken.Int64 => typeof(KeyValuePair<TKey, long>),
            _ when token == DBusSignatureToken.UInt64 => typeof(KeyValuePair<TKey, ulong>),
            _ when token == DBusSignatureToken.Double => typeof(KeyValuePair<TKey, double>),
            _ when token == DBusSignatureToken.String => typeof(KeyValuePair<TKey, string>),
            _ when token == DBusSignatureToken.ObjectPath => typeof(KeyValuePair<TKey, DBusObjectPath>),
            _ when token == DBusSignatureToken.Signature => typeof(KeyValuePair<TKey, DBusSignature>),
            _ when token == DBusSignatureToken.UnixFd => typeof(KeyValuePair<TKey, DBusUnixFd>),
            _ when token == DBusSignatureToken.Variant => typeof(KeyValuePair<TKey, DBusVariant>),
            _ when token == DBusSignatureToken.StructBegin => typeof(KeyValuePair<TKey, DBusStruct>),
            _ => typeof(KeyValuePair<TKey, object>)
        };
    }

    private static string InferStructSignature(DBusStruct dbusStruct)
    {
        var parts = new List<string>(dbusStruct.Count);
        parts.AddRange(dbusStruct.Select(InferSignatureFromValue));

        return string.Concat(DBusSignatureToken.StructBegin, string.Concat(parts), DBusSignatureToken.StructEnd);
    }

    private static string InferArrayElementSignature(object array, Type elementType)
    {
        if (elementType == typeof(DBusStruct))
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
}
