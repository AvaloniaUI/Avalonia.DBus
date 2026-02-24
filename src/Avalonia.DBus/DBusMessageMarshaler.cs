using System;
using System.Collections;
using System.Collections.Generic;
using Avalonia.DBus.Native;
using static Avalonia.DBus.DbusHelpers;
using static Avalonia.DBus.Native.LibDbus;
using DBusNativeMessage = Avalonia.DBus.Native.DBusMessage;

namespace Avalonia.DBus;

internal static unsafe class DBusMessageMarshaler
{
    public static DBusNativeMessage* ToNative(DBusMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var native = dbus_message_new((int)message.Type);
        if (native == null)
        {
            throw new InvalidOperationException("Failed to create D-Bus message.");
        }

        try
        {
            ApplyHeaders(message, native);
            AppendBody(message, native);
            return native;
        }
        catch
        {
            dbus_message_unref(native);
            throw;
        }
    }

    public static DBusMessage FromNative(DBusNativeMessage* message)
    {
        var signature = PtrToString(dbus_message_get_signature(message));
        var body = ReadBody(message, signature);

        var managed = new DBusMessage
        {
            Type = (DBusMessageType)dbus_message_get_type(message),
            Flags = ReadFlags(message),
            Serial = dbus_message_get_serial(message),
            ReplySerial = dbus_message_get_reply_serial(message),
            Path = ReadObjectPath(message),
            Interface = PtrToStringNullable(dbus_message_get_interface(message)),
            Member = PtrToStringNullable(dbus_message_get_member(message)),
            ErrorName = PtrToStringNullable(dbus_message_get_error_name(message)),
            Destination = PtrToStringNullable(dbus_message_get_destination(message)),
            Sender = PtrToStringNullable(dbus_message_get_sender(message))
        };

        managed.SetBodyWithSignature(body, signature);
        return managed;
    }

    private static void ApplyHeaders(DBusMessage message, DBusNativeMessage* native)
    {
        if (message.Path.HasValue)
        {
            using var path = new Utf8String(message.Path.Value.Value);
            dbus_message_set_path(native, path.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Interface))
        {
            using var iface = new Utf8String(message.Interface);
            dbus_message_set_interface(native, iface.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Member))
        {
            using var member = new Utf8String(message.Member);
            dbus_message_set_member(native, member.Pointer);
        }

        if (!string.IsNullOrEmpty(message.ErrorName))
        {
            using var error = new Utf8String(message.ErrorName);
            dbus_message_set_error_name(native, error.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Destination))
        {
            using var destination = new Utf8String(message.Destination);
            dbus_message_set_destination(native, destination.Pointer);
        }

        if (message.ReplySerial != 0)
        {
            dbus_message_set_reply_serial(native, message.ReplySerial);
        }

        ApplyFlags(message.Flags, native);
    }

    private static void ApplyFlags(DBusMessageFlags flags, DBusNativeMessage* native)
    {
        dbus_message_set_no_reply(native, flags.HasFlag(DBusMessageFlags.NoReplyExpected) ? 1u : 0u);
        dbus_message_set_auto_start(native, flags.HasFlag(DBusMessageFlags.NoAutoStart) ? 0u : 1u);
        dbus_message_set_allow_interactive_authorization(
            native,
            flags.HasFlag(DBusMessageFlags.AllowInteractiveAuthorization) ? 1u : 0u);
    }

    private static DBusMessageFlags ReadFlags(DBusNativeMessage* message)
    {
        var flags = DBusMessageFlags.None;
        if (dbus_message_get_no_reply(message) != 0)
        {
            flags |= DBusMessageFlags.NoReplyExpected;
        }
        if (dbus_message_get_auto_start(message) == 0)
        {
            flags |= DBusMessageFlags.NoAutoStart;
        }
        if (dbus_message_get_allow_interactive_authorization(message) != 0)
        {
            flags |= DBusMessageFlags.AllowInteractiveAuthorization;
        }

        return flags;
    }

    private static DBusObjectPath? ReadObjectPath(DBusNativeMessage* message)
    {
        var path = PtrToStringNullable(dbus_message_get_path(message));
        if (path == null)
            return null;
        return new DBusObjectPath(path);
    }

    private static IReadOnlyList<object> ReadBody(DBusNativeMessage* message, string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return [];
        }

        DBusMessageIter iter;
        if (dbus_message_iter_init(message, &iter) == 0)
        {
            return [];
        }

        var items = new List<object>();
        var index = 0;
        while (index < signature.Length)
        {
            var typeSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
            items.Add(ReadValue(typeSignature, ref iter));
        }

        return items;
    }

    private static object ReadValue(string signature, ref DBusMessageIter iter)
    {
        if (string.IsNullOrEmpty(signature))
        {
            throw new ArgumentException("Signature is required.", nameof(signature));
        }

        DBusSignatureToken token = signature[0];
        if (token == DBusSignatureToken.Byte)
        {
            return ReadBasic<byte>(ref iter);
        }
        if (token == DBusSignatureToken.Boolean)
        {
            return ReadBool(ref iter);
        }
        if (token == DBusSignatureToken.Int16)
        {
            return ReadBasic<short>(ref iter);
        }
        if (token == DBusSignatureToken.UInt16)
        {
            return ReadBasic<ushort>(ref iter);
        }
        if (token == DBusSignatureToken.Int32)
        {
            return ReadBasic<int>(ref iter);
        }
        if (token == DBusSignatureToken.UInt32)
        {
            return ReadBasic<uint>(ref iter);
        }
        if (token == DBusSignatureToken.Int64)
        {
            return ReadBasic<long>(ref iter);
        }
        if (token == DBusSignatureToken.UInt64)
        {
            return ReadBasic<ulong>(ref iter);
        }
        if (token == DBusSignatureToken.Double)
        {
            return ReadBasic<double>(ref iter);
        }
        if (token == DBusSignatureToken.String)
        {
            return ReadString(ref iter);
        }
        if (token == DBusSignatureToken.ObjectPath)
        {
            return new DBusObjectPath(ReadString(ref iter));
        }
        if (token == DBusSignatureToken.Signature)
        {
            return new DBusSignature(ReadString(ref iter));
        }
        if (token == DBusSignatureToken.UnixFd)
        {
            return new DBusUnixFd(ReadBasic<int>(ref iter));
        }
        if (token == DBusSignatureToken.Variant)
        {
            return ReadVariant(ref iter);
        }
        if (token == DBusSignatureToken.Array)
        {
            return ReadArray(signature, ref iter);
        }
        if (token == DBusSignatureToken.StructBegin)
        {
            return ReadStruct(signature, ref iter);
        }
        if (token == DBusSignatureToken.DictEntryBegin)
        {
            return ReadDictEntry(signature, ref iter);
        }

        throw new NotSupportedException($"Unsupported signature token: {signature[0]}");
    }

    private static T ReadBasic<T>(ref DBusMessageIter iter) where T : unmanaged
    {
        T value;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_get_basic(iterPtr, &value);
            dbus_message_iter_next(iterPtr);
        }
        return value;
    }

    private static bool ReadBool(ref DBusMessageIter iter)
    {
        uint value;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_get_basic(iterPtr, &value);
            dbus_message_iter_next(iterPtr);
        }
        return value != 0;
    }

    private static string ReadString(ref DBusMessageIter iter)
    {
        byte* value;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_get_basic(iterPtr, &value);
            dbus_message_iter_next(iterPtr);
        }
        return PtrToString(value);
    }

    private static object ReadVariant(ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_recurse(iterPtr, &child);
        }

        var childPtr = &child;
        var signaturePtr = dbus_message_iter_get_signature(childPtr);

        var signature = signaturePtr == null ? string.Empty : PtrToString(signaturePtr);
        if (signaturePtr != null)
        {
            NativeMethods.dbus_free(signaturePtr);
        }

        if (string.IsNullOrEmpty(signature))
        {
            signature = InferSignatureFromIter(ref child);
        }

        var value = ReadValue(signature, ref child);

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_next(iterPtr);
        }

        return new DBusVariant(value);
    }

    private static string InferSignatureFromIter(ref DBusMessageIter iter)
    {
        int argType;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            argType = dbus_message_iter_get_arg_type(iterPtr);
        }

        return argType switch
        {
            DBUS_TYPE_BYTE => DBusSignatureToken.Byte,
            DBUS_TYPE_BOOLEAN => DBusSignatureToken.Boolean,
            DBUS_TYPE_INT16 => DBusSignatureToken.Int16,
            DBUS_TYPE_UINT16 => DBusSignatureToken.UInt16,
            DBUS_TYPE_INT32 => DBusSignatureToken.Int32,
            DBUS_TYPE_UINT32 => DBusSignatureToken.UInt32,
            DBUS_TYPE_INT64 => DBusSignatureToken.Int64,
            DBUS_TYPE_UINT64 => DBusSignatureToken.UInt64,
            DBUS_TYPE_DOUBLE => DBusSignatureToken.Double,
            DBUS_TYPE_STRING => DBusSignatureToken.String,
            DBUS_TYPE_OBJECT_PATH => DBusSignatureToken.ObjectPath,
            DBUS_TYPE_SIGNATURE => DBusSignatureToken.Signature,
            DBUS_TYPE_UNIX_FD => DBusSignatureToken.UnixFd,
            DBUS_TYPE_VARIANT => DBusSignatureToken.Variant,
            _ => string.Empty
        };
    }

    private static object ReadArray(string signature, ref DBusMessageIter iter)
    {
        var index = 1;
        var elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
        if (elementSignature.Length > 0 && elementSignature[0] == DBusSignatureToken.DictEntryBegin)
        {
            return ReadDictionaryArray(elementSignature, ref iter);
        }

        return ReadArrayItems(elementSignature, ref iter);
    }

    private static object ReadArrayItems(string elementSignature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_recurse(iterPtr, &child);
        }

        var items = new List<object>();
        while (dbus_message_iter_get_arg_type(&child) != DBUS_TYPE_INVALID)
        {
            items.Add(ReadValue(elementSignature, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_next(iterPtr);
        }

        return CreateArrayInstance(elementSignature, items);
    }

    private static object ReadDictionaryArray(string entrySignature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_recurse(iterPtr, &child);
        }

        var entries = new List<KeyValuePair<object?, object?>>();
        while (dbus_message_iter_get_arg_type(&child) != DBUS_TYPE_INVALID)
        {
            entries.Add(ReadDictEntry(entrySignature, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_next(iterPtr);
        }

        return CreateDictInstance(entrySignature, entries);
    }

    private static object ReadStruct(string signature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_recurse(iterPtr, &child);
        }

        var partSignatures = DBusSignatureParser.ParseStructSignatures(signature);
        var values = new List<object>(partSignatures.Count);
        foreach (var part in partSignatures)
        {
            values.Add(ReadValue(part, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_next(iterPtr);
        }

        return new DBusStruct(values);
    }

    private static KeyValuePair<object?, object?> ReadDictEntry(string signature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_recurse(iterPtr, &child);
        }

        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
        var key = ReadValue(keySig, ref child);
        var value = ReadValue(valueSig, ref child);

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_next(iterPtr);
        }

        return new KeyValuePair<object?, object?>(key, value);
    }

    private static object CreateArrayInstance(string elementSignature, List<object> items)
    {
        DBusSignatureToken token = elementSignature[0];
        return token switch
        {
            _ when token == DBusSignatureToken.Byte => CreateList<byte>(items),
            _ when token == DBusSignatureToken.Boolean => CreateList<bool>(items),
            _ when token == DBusSignatureToken.Int16 => CreateList<short>(items),
            _ when token == DBusSignatureToken.UInt16 => CreateList<ushort>(items),
            _ when token == DBusSignatureToken.Int32 => CreateList<int>(items),
            _ when token == DBusSignatureToken.UInt32 => CreateList<uint>(items),
            _ when token == DBusSignatureToken.Int64 => CreateList<long>(items),
            _ when token == DBusSignatureToken.UInt64 => CreateList<ulong>(items),
            _ when token == DBusSignatureToken.Double => CreateList<double>(items),
            _ when token == DBusSignatureToken.String => CreateList<string>(items),
            _ when token == DBusSignatureToken.ObjectPath => CreateList<DBusObjectPath>(items),
            _ when token == DBusSignatureToken.Signature => CreateList<DBusSignature>(items),
            _ when token == DBusSignatureToken.UnixFd => CreateList<DBusUnixFd>(items),
            _ when token == DBusSignatureToken.Variant => CreateList<DBusVariant>(items),
            _ when token == DBusSignatureToken.StructBegin => CreateList<DBusStruct>(items),
            _ when token == DBusSignatureToken.Array => CreateList<object>(items),
            _ => CreateList<object>(items)
        };
    }

    private static object CreateDictInstance(string entrySignature, List<KeyValuePair<object?, object?>> entries)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        return keySig[0] switch
        {
            _ when keySig[0] == DBusSignatureToken.Byte => CreateDictionaryForValue<byte>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Boolean => CreateDictionaryForValue<bool>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Int16 => CreateDictionaryForValue<short>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.UInt16 => CreateDictionaryForValue<ushort>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Int32 => CreateDictionaryForValue<int>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.UInt32 => CreateDictionaryForValue<uint>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Int64 => CreateDictionaryForValue<long>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.UInt64 => CreateDictionaryForValue<ulong>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Double => CreateDictionaryForValue<double>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.String => CreateDictionaryForValue<string>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.ObjectPath => CreateDictionaryForValue<DBusObjectPath>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.Signature => CreateDictionaryForValue<DBusSignature>(valueSig, entries),
            _ when keySig[0] == DBusSignatureToken.UnixFd => CreateDictionaryForValue<DBusUnixFd>(valueSig, entries),
            _ => CreateDictionary<object, object?>(entries)
        };
    }

    private static List<T> CreateList<T>(IReadOnlyList<object> items)
    {
        var list = new List<T>(items.Count);
        foreach (var item in items)
        {
            list.Add((T)item);
        }

        return list;
    }

    private static object CreateDictionaryForValue<TKey>(
        string valueSignature,
        IReadOnlyList<KeyValuePair<object?, object?>> entries) where TKey : notnull
    {
        DBusSignatureToken token = valueSignature[0];
        if (token == DBusSignatureToken.Array)
        {
            var index = 1;
            var elementSignature = DBusSignatureParser.ReadSingleType(valueSignature, ref index);
            return elementSignature[0] switch
            {
                _ when elementSignature[0] == DBusSignatureToken.Byte => CreateDictionary<TKey, List<byte>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Boolean => CreateDictionary<TKey, List<bool>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Int16 => CreateDictionary<TKey, List<short>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.UInt16 => CreateDictionary<TKey, List<ushort>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Int32 => CreateDictionary<TKey, List<int>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.UInt32 => CreateDictionary<TKey, List<uint>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Int64 => CreateDictionary<TKey, List<long>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.UInt64 => CreateDictionary<TKey, List<ulong>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Double => CreateDictionary<TKey, List<double>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.String => CreateDictionary<TKey, List<string>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.ObjectPath => CreateDictionary<TKey, List<DBusObjectPath>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Signature => CreateDictionary<TKey, List<DBusSignature>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.UnixFd => CreateDictionary<TKey, List<DBusUnixFd>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.Variant => CreateDictionary<TKey, List<DBusVariant>>(entries),
                _ when elementSignature[0] == DBusSignatureToken.StructBegin => CreateDictionary<TKey, List<DBusStruct>>(entries),
                _ => CreateDictionary<TKey, object?>(entries)
            };
        }

        return token switch
        {
            _ when token == DBusSignatureToken.Byte => CreateDictionary<TKey, byte>(entries),
            _ when token == DBusSignatureToken.Boolean => CreateDictionary<TKey, bool>(entries),
            _ when token == DBusSignatureToken.Int16 => CreateDictionary<TKey, short>(entries),
            _ when token == DBusSignatureToken.UInt16 => CreateDictionary<TKey, ushort>(entries),
            _ when token == DBusSignatureToken.Int32 => CreateDictionary<TKey, int>(entries),
            _ when token == DBusSignatureToken.UInt32 => CreateDictionary<TKey, uint>(entries),
            _ when token == DBusSignatureToken.Int64 => CreateDictionary<TKey, long>(entries),
            _ when token == DBusSignatureToken.UInt64 => CreateDictionary<TKey, ulong>(entries),
            _ when token == DBusSignatureToken.Double => CreateDictionary<TKey, double>(entries),
            _ when token == DBusSignatureToken.String => CreateDictionary<TKey, string>(entries),
            _ when token == DBusSignatureToken.ObjectPath => CreateDictionary<TKey, DBusObjectPath>(entries),
            _ when token == DBusSignatureToken.Signature => CreateDictionary<TKey, DBusSignature>(entries),
            _ when token == DBusSignatureToken.UnixFd => CreateDictionary<TKey, DBusUnixFd>(entries),
            _ when token == DBusSignatureToken.Variant => CreateDictionary<TKey, DBusVariant>(entries),
            _ when token == DBusSignatureToken.StructBegin => CreateDictionary<TKey, DBusStruct>(entries),
            _ => CreateDictionary<TKey, object?>(entries)
        };
    }

    private static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>(
        IReadOnlyList<KeyValuePair<object?, object?>> entries) where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Key is null)
            {
                throw new InvalidOperationException("Dictionary contains null keys.");
            }

            var value = entry.Value is null ? default! : (TValue)entry.Value;
            dict.Add((TKey)entry.Key, value);
        }

        return dict;
    }

    private static void AppendBody(DBusMessage message, DBusNativeMessage* native)
    {
        if (message.Body.Count == 0)
        {
            return;
        }

        DBusMessageIter iter;
        dbus_message_iter_init_append(native, &iter);

        foreach (var item in message.Body)
        {
            AppendValue(ref iter, item);
        }
    }

    private static void AppendValue(ref DBusMessageIter iter, object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (value)
        {
            case byte byteValue:
                AppendBasic(ref iter, DBUS_TYPE_BYTE, byteValue);
                return;
            case bool boolValue:
                var dbusBool = boolValue ? 1u : 0u;
                AppendBasic(ref iter, DBUS_TYPE_BOOLEAN, dbusBool);
                return;
            case short int16Value:
                AppendBasic(ref iter, DBUS_TYPE_INT16, int16Value);
                return;
            case ushort uint16Value:
                AppendBasic(ref iter, DBUS_TYPE_UINT16, uint16Value);
                return;
            case int int32Value:
                AppendBasic(ref iter, DBUS_TYPE_INT32, int32Value);
                return;
            case uint uint32Value:
                AppendBasic(ref iter, DBUS_TYPE_UINT32, uint32Value);
                return;
            case long int64Value:
                AppendBasic(ref iter, DBUS_TYPE_INT64, int64Value);
                return;
            case ulong uint64Value:
                AppendBasic(ref iter, DBUS_TYPE_UINT64, uint64Value);
                return;
            case double doubleValue:
                AppendBasic(ref iter, DBUS_TYPE_DOUBLE, doubleValue);
                return;
            case string stringValue:
                AppendString(ref iter, DBUS_TYPE_STRING, stringValue);
                return;
            case DBusObjectPath objectPath:
                AppendString(ref iter, DBUS_TYPE_OBJECT_PATH, objectPath.Value);
                return;
            case DBusSignature signature:
                AppendString(ref iter, DBUS_TYPE_SIGNATURE, signature.Value);
                return;
            case DBusUnixFd unixFd:
                AppendBasic(ref iter, DBUS_TYPE_UNIX_FD, unixFd.Fd);
                return;
            case DBusVariant variant:
                AppendVariant(ref iter, variant);
                return;
            case DBusStruct dbusStruct:
                AppendStruct(ref iter, dbusStruct);
                return;
            case IDBusStructConvertible structConvertible:
                AppendStruct(ref iter, structConvertible.ToDbusStruct());
                return;
            default:
                if (DBusCollectionHelpers.TryGetDictionaryTypes(value.GetType(), out _, out _) || value is IDictionary)
                {
                    AppendDict(ref iter, value);
                    return;
                }

                if (DBusCollectionHelpers.TryGetListElementType(value.GetType(), out _) || value is IList)
                {
                    AppendArray(ref iter, value);
                    return;
                }

                throw new NotSupportedException($"Unsupported D-Bus value type: {value.GetType().FullName}");
        }
    }

    private static void AppendBasic<T>(ref DBusMessageIter iter, int dbusType, T value) where T : unmanaged
    {
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_append_basic(iterPtr, dbusType, &value);
        }
    }

    private static void AppendString(ref DBusMessageIter iter, int dbusType, string value)
    {
        using var utf8 = new Utf8String(value ?? string.Empty);
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            var ptr = utf8.Pointer;
            dbus_message_iter_append_basic(iterPtr, dbusType, &ptr);
        }
    }

    private static void AppendArray(ref DBusMessageIter iter, object array)
    {
        var arraySignature = DBusSignatureInference.InferSignatureFromValue(array);
        if (arraySignature.Length < 2 || arraySignature[0] != DBusSignatureToken.Array)
        {
            throw new InvalidOperationException("Invalid array signature.");
        }

        var elementSignature = arraySignature.Substring(1);
        using var sig = new Utf8String(elementSignature);
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            if (dbus_message_iter_open_container(iterPtr, DBUS_TYPE_ARRAY, sig.Pointer, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open array container.");
            }
        }

        foreach (var item in DBusCollectionHelpers.EnumerateListItems(array))
        {
            AppendValue(ref child, item ?? throw new InvalidOperationException("Array contains null values."));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_close_container(iterPtr, &child);
        }
    }

    private static void AppendDict(ref DBusMessageIter iter, object dict)
    {
        var arraySignature = DBusSignatureInference.InferSignatureFromValue(dict);
        if (arraySignature.Length < 3
            || arraySignature[0] != DBusSignatureToken.Array
            || arraySignature[1] != DBusSignatureToken.DictEntryBegin)
        {
            throw new InvalidOperationException("Invalid dictionary signature.");
        }

        var entrySignature = arraySignature.Substring(1);
        using var sig = new Utf8String(entrySignature);
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            if (dbus_message_iter_open_container(iterPtr, DBUS_TYPE_ARRAY, sig.Pointer, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open dictionary container.");
            }
        }

        foreach (var entry in DBusCollectionHelpers.EnumerateDictionaryEntries(dict))
        {
            DBusMessageIter entryIter;
            var childPtr = &child;
            if (dbus_message_iter_open_container(childPtr, DBUS_TYPE_DICT_ENTRY, null, &entryIter) == 0)
            {
                throw new InvalidOperationException("Failed to open dictionary entry container.");
            }

            AppendValue(ref entryIter, entry.Key ?? throw new InvalidOperationException("Dictionary contains null keys."));
            AppendValue(ref entryIter, entry.Value ?? throw new InvalidOperationException("Dictionary contains null values."));

            dbus_message_iter_close_container(childPtr, &entryIter);
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_close_container(iterPtr, &child);
        }
    }

    private static void AppendStruct(ref DBusMessageIter iter, DBusStruct dbusStruct)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            if (dbus_message_iter_open_container(iterPtr, DBUS_TYPE_STRUCT, null, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open struct container.");
            }
        }

        foreach (var field in dbusStruct)
        {
            AppendValue(ref child, field);
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_close_container(iterPtr, &child);
        }
    }

    private static void AppendVariant(ref DBusMessageIter iter, DBusVariant variant)
    {
        var signature = variant.Signature.Value;
        if (string.IsNullOrEmpty(signature))
        {
            signature = DBusSignatureInference.InferSignatureFromValue(variant.Value);
        }

        using var sig = new Utf8String(signature);
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            if (dbus_message_iter_open_container(iterPtr, DBUS_TYPE_VARIANT, sig.Pointer, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open variant container.");
            }
        }

        AppendValue(ref child, variant.Value);

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            dbus_message_iter_close_container(iterPtr, &child);
        }
    }

}
