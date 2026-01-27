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
                return "y";
            case bool:
                return "b";
            case short:
                return "n";
            case ushort:
                return "q";
            case int:
                return "i";
            case uint:
                return "u";
            case long:
                return "x";
            case ulong:
                return "t";
            case double:
                return "d";
            case string:
                return "s";
            case DBusObjectPath:
                return "o";
            case DBusSignature:
                return "g";
            case DBusUnixFd:
                return "h";
            case DBusVariant:
                return "v";
            case DBusStruct dbusStruct:
                return InferStructSignature(dbusStruct);
            case IDBusArray dbusArray:
                return "a" + InferArrayElementSignature(dbusArray);
            case IDBusDict dbusDict:
                return "a{" + InferDictKeySignature(dbusDict) + InferDictValueSignature(dbusDict) + "}";
            default:
                throw new NotSupportedException($"Unsupported D-Bus value type: {value.GetType().FullName}");
        }
    }

    internal static string InferSignatureFromType(Type type)
    {
        if (type == typeof(byte))
        {
            return "y";
        }
        if (type == typeof(bool))
        {
            return "b";
        }
        if (type == typeof(short))
        {
            return "n";
        }
        if (type == typeof(ushort))
        {
            return "q";
        }
        if (type == typeof(int))
        {
            return "i";
        }
        if (type == typeof(uint))
        {
            return "u";
        }
        if (type == typeof(long))
        {
            return "x";
        }
        if (type == typeof(ulong))
        {
            return "t";
        }
        if (type == typeof(double))
        {
            return "d";
        }
        if (type == typeof(string))
        {
            return "s";
        }
        if (type == typeof(DBusObjectPath))
        {
            return "o";
        }
        if (type == typeof(DBusSignature))
        {
            return "g";
        }
        if (type == typeof(DBusUnixFd))
        {
            return "h";
        }
        if (type == typeof(DBusVariant))
        {
            return "v";
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
                return "a" + InferSignatureFromType(elementType);
            }
            if (genericType == typeof(DBusDict<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                return "a{" + InferSignatureFromType(keyType) + InferSignatureFromType(valueType) + "}";
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

        char token = signature[0];
        switch (token)
        {
            case 'y':
                return typeof(byte);
            case 'b':
                return typeof(bool);
            case 'n':
                return typeof(short);
            case 'q':
                return typeof(ushort);
            case 'i':
                return typeof(int);
            case 'u':
                return typeof(uint);
            case 'x':
                return typeof(long);
            case 't':
                return typeof(ulong);
            case 'd':
                return typeof(double);
            case 's':
                return typeof(string);
            case 'o':
                return typeof(DBusObjectPath);
            case 'g':
                return typeof(DBusSignature);
            case 'h':
                return typeof(DBusUnixFd);
            case 'v':
                return typeof(DBusVariant);
            case 'a':
            {
                int index = 1;
                string elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                if (elementSignature.Length > 0 && elementSignature[0] == '{')
                {
                    var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(elementSignature);
                    Type keyType = GetTypeForSignature(keySig);
                    Type valueType = GetTypeForSignature(valueSig);
                    return typeof(DBusDict<,>).MakeGenericType(keyType, valueType);
                }

                Type elementType = GetTypeForSignature(elementSignature);
                return typeof(DBusArray<>).MakeGenericType(elementType);
            }
            case '(':
                return typeof(DBusStruct);
            case '{':
            {
                var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
                Type keyType = GetTypeForSignature(keySig);
                Type valueType = GetTypeForSignature(valueSig);
                return typeof(KeyValuePair<,>).MakeGenericType(keyType, valueType);
            }
            default:
                throw new NotSupportedException($"Unsupported D-Bus signature token: {token}");
        }
    }

    private static string InferStructSignature(DBusStruct dbusStruct)
    {
        var parts = new List<string>(dbusStruct.Count);
        foreach (var field in dbusStruct)
        {
            parts.Add(InferSignatureFromValue(field));
        }

        return "(" + string.Concat(parts) + ")";
    }

    private static string InferArrayElementSignature(IDBusArray array)
    {
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
