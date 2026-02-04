using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using Avalonia.DBus.Native;
using DBusNativeMessage = Avalonia.DBus.Native.DBusMessage;
using LibDbus = Avalonia.DBus.Native.LibDbus;

namespace Avalonia.DBus;

internal static unsafe class DBusMessageMarshaler
{
    public static DBusNativeMessage* ToNative(DBusMessage message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var native = LibDbus.dbus_message_new((int)message.Type);
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
            LibDbus.dbus_message_unref(native);
            throw;
        }
    }

    public static DBusMessage FromNative(DBusNativeMessage* message)
    {
        var signature = DbusHelpers.PtrToString(LibDbus.dbus_message_get_signature(message));
        var body = ReadBody(message, signature);

        var managed = new DBusMessage
        {
            Type = (DBusMessageType)LibDbus.dbus_message_get_type(message),
            Flags = ReadFlags(message),
            Serial = LibDbus.dbus_message_get_serial(message),
            ReplySerial = LibDbus.dbus_message_get_reply_serial(message),
            Path = ReadObjectPath(message),
            Interface = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_interface(message)),
            Member = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_member(message)),
            ErrorName = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_error_name(message)),
            Destination = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_destination(message)),
            Sender = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_sender(message))
        };

        managed.SetBodyWithSignature(body, signature);
        return managed;
    }

    private static void ApplyHeaders(DBusMessage message, DBusNativeMessage* native)
    {
        if (message.Path.HasValue)
        {
            using var path = new Utf8String(message.Path.Value.Value);
            LibDbus.dbus_message_set_path(native, path.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Interface))
        {
            using var iface = new Utf8String(message.Interface);
            LibDbus.dbus_message_set_interface(native, iface.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Member))
        {
            using var member = new Utf8String(message.Member);
            LibDbus.dbus_message_set_member(native, member.Pointer);
        }

        if (!string.IsNullOrEmpty(message.ErrorName))
        {
            using var error = new Utf8String(message.ErrorName);
            LibDbus.dbus_message_set_error_name(native, error.Pointer);
        }

        if (!string.IsNullOrEmpty(message.Destination))
        {
            using var destination = new Utf8String(message.Destination);
            LibDbus.dbus_message_set_destination(native, destination.Pointer);
        }

        if (message.ReplySerial != 0)
        {
            LibDbus.dbus_message_set_reply_serial(native, message.ReplySerial);
        }

        ApplyFlags(message.Flags, native);
    }

    private static void ApplyFlags(DBusMessageFlags flags, DBusNativeMessage* native)
    {
        LibDbus.dbus_message_set_no_reply(native, flags.HasFlag(DBusMessageFlags.NoReplyExpected) ? 1u : 0u);
        LibDbus.dbus_message_set_auto_start(native, flags.HasFlag(DBusMessageFlags.NoAutoStart) ? 0u : 1u);
        LibDbus.dbus_message_set_allow_interactive_authorization(
            native,
            flags.HasFlag(DBusMessageFlags.AllowInteractiveAuthorization) ? 1u : 0u);
    }

    private static DBusMessageFlags ReadFlags(DBusNativeMessage* message)
    {
        var flags = DBusMessageFlags.None;
        if (LibDbus.dbus_message_get_no_reply(message) != 0)
        {
            flags |= DBusMessageFlags.NoReplyExpected;
        }
        if (LibDbus.dbus_message_get_auto_start(message) == 0)
        {
            flags |= DBusMessageFlags.NoAutoStart;
        }
        if (LibDbus.dbus_message_get_allow_interactive_authorization(message) != 0)
        {
            flags |= DBusMessageFlags.AllowInteractiveAuthorization;
        }

        return flags;
    }

    private static DBusObjectPath? ReadObjectPath(DBusNativeMessage* message)
    {
        var path = DbusHelpers.PtrToStringNullable(LibDbus.dbus_message_get_path(message));
        return path == null ? null : new DBusObjectPath(path);
    }

    private static IReadOnlyList<object> ReadBody(DBusNativeMessage* message, string signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return [];
        }

        DBusMessageIter iter;
        if (LibDbus.dbus_message_iter_init(message, &iter) == 0)
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
            LibDbus.dbus_message_iter_get_basic(iterPtr, &value);
            LibDbus.dbus_message_iter_next(iterPtr);
        }
        return value;
    }

    private static bool ReadBool(ref DBusMessageIter iter)
    {
        uint value;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_get_basic(iterPtr, &value);
            LibDbus.dbus_message_iter_next(iterPtr);
        }
        return value != 0;
    }

    private static string ReadString(ref DBusMessageIter iter)
    {
        byte* value;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_get_basic(iterPtr, &value);
            LibDbus.dbus_message_iter_next(iterPtr);
        }
        return DbusHelpers.PtrToString(value);
    }

    private static object ReadVariant(ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_recurse(iterPtr, &child);
        }

        var childPtr = &child;
        var signaturePtr = LibDbus.dbus_message_iter_get_signature(childPtr);

        var signature = signaturePtr == null ? string.Empty : DbusHelpers.PtrToString(signaturePtr);
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
            LibDbus.dbus_message_iter_next(iterPtr);
        }

        return new DBusVariant(new DBusSignature(signature), value);
    }

    private static string InferSignatureFromIter(ref DBusMessageIter iter)
    {
        int argType;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            argType = LibDbus.dbus_message_iter_get_arg_type(iterPtr);
        }

        return argType switch
        {
            LibDbus.DBUS_TYPE_BYTE => DBusSignatureToken.Byte,
            LibDbus.DBUS_TYPE_BOOLEAN => DBusSignatureToken.Boolean,
            LibDbus.DBUS_TYPE_INT16 => DBusSignatureToken.Int16,
            LibDbus.DBUS_TYPE_UINT16 => DBusSignatureToken.UInt16,
            LibDbus.DBUS_TYPE_INT32 => DBusSignatureToken.Int32,
            LibDbus.DBUS_TYPE_UINT32 => DBusSignatureToken.UInt32,
            LibDbus.DBUS_TYPE_INT64 => DBusSignatureToken.Int64,
            LibDbus.DBUS_TYPE_UINT64 => DBusSignatureToken.UInt64,
            LibDbus.DBUS_TYPE_DOUBLE => DBusSignatureToken.Double,
            LibDbus.DBUS_TYPE_STRING => DBusSignatureToken.String,
            LibDbus.DBUS_TYPE_OBJECT_PATH => DBusSignatureToken.ObjectPath,
            LibDbus.DBUS_TYPE_SIGNATURE => DBusSignatureToken.Signature,
            LibDbus.DBUS_TYPE_UNIX_FD => DBusSignatureToken.UnixFd,
            LibDbus.DBUS_TYPE_VARIANT => DBusSignatureToken.Variant,
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
            LibDbus.dbus_message_iter_recurse(iterPtr, &child);
        }

        var items = new List<object>();
        while (LibDbus.dbus_message_iter_get_arg_type(&child) != LibDbus.DBUS_TYPE_INVALID)
        {
            items.Add(ReadValue(elementSignature, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_next(iterPtr);
        }

        return CreateArrayInstance(elementSignature, items);
    }

    private static object ReadDictionaryArray(string entrySignature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_recurse(iterPtr, &child);
        }

        var entries = new List<KeyValuePair<object?, object?>>();
        while (LibDbus.dbus_message_iter_get_arg_type(&child) != LibDbus.DBUS_TYPE_INVALID)
        {
            entries.Add(ReadDictEntry(entrySignature, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_next(iterPtr);
        }

        return CreateDictInstance(entrySignature, entries);
    }

    private static object ReadStruct(string signature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_recurse(iterPtr, &child);
        }

        var partSignatures = DBusSignatureParser.ParseStructSignatures(signature);
        var values = new List<object>(partSignatures.Count);
        foreach (var part in partSignatures)
        {
            values.Add(ReadValue(part, ref child));
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_next(iterPtr);
        }

        return new DBusStruct(values);
    }

    private static KeyValuePair<object?, object?> ReadDictEntry(string signature, ref DBusMessageIter iter)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_recurse(iterPtr, &child);
        }

        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(signature);
        var key = ReadValue(keySig, ref child);
        var value = ReadValue(valueSig, ref child);

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_next(iterPtr);
        }

        return new KeyValuePair<object?, object?>(key, value);
    }

    private static object CreateArrayInstance(string elementSignature, List<object> items)
    {
        var elementType = DBusSignatureInference.GetTypeForSignature(elementSignature);
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    private static object CreateDictInstance(string entrySignature, List<KeyValuePair<object?, object?>> entries)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        var keyType = DBusSignatureInference.GetTypeForSignature(keySig);
        var valueType = DBusSignatureInference.GetTypeForSignature(valueSig);

        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;

        foreach (var entry in entries)
        {
            if (entry.Key is null)
            {
                throw new InvalidOperationException("Dictionary contains null keys.");
            }

            dict.Add(entry.Key, entry.Value);
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
        LibDbus.dbus_message_iter_init_append(native, &iter);

        foreach (var item in message.Body)
        {
            AppendValue(ref iter, item);
        }
    }

    private static void AppendValue(ref DBusMessageIter iter, object value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        switch (value)
        {
            case byte byteValue:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_BYTE, byteValue);
                return;
            case bool boolValue:
                var dbusBool = boolValue ? 1u : 0u;
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_BOOLEAN, dbusBool);
                return;
            case short int16Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_INT16, int16Value);
                return;
            case ushort uint16Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_UINT16, uint16Value);
                return;
            case int int32Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_INT32, int32Value);
                return;
            case uint uint32Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_UINT32, uint32Value);
                return;
            case long int64Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_INT64, int64Value);
                return;
            case ulong uint64Value:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_UINT64, uint64Value);
                return;
            case double doubleValue:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_DOUBLE, doubleValue);
                return;
            case string stringValue:
                AppendString(ref iter, LibDbus.DBUS_TYPE_STRING, stringValue);
                return;
            case DBusObjectPath objectPath:
                AppendString(ref iter, LibDbus.DBUS_TYPE_OBJECT_PATH, objectPath.Value);
                return;
            case DBusSignature signature:
                AppendString(ref iter, LibDbus.DBUS_TYPE_SIGNATURE, signature.Value);
                return;
            case DBusUnixFd unixFd:
                AppendBasic(ref iter, LibDbus.DBUS_TYPE_UNIX_FD, unixFd.Fd);
                return;
            case DBusVariant variant:
                AppendVariant(ref iter, variant);
                return;
            case DBusStruct dbusStruct:
                AppendStruct(ref iter, dbusStruct);
                return;
            default:
                if (TryConvertToDbusStruct(value, out var convertedStruct))
                {
                    AppendStruct(ref iter, convertedStruct);
                    return;
                }

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
            LibDbus.dbus_message_iter_append_basic(iterPtr, dbusType, &value);
        }
    }

    private static void AppendString(ref DBusMessageIter iter, int dbusType, string value)
    {
        using var utf8 = new Utf8String(value ?? string.Empty);
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            var ptr = utf8.Pointer;
            LibDbus.dbus_message_iter_append_basic(iterPtr, dbusType, &ptr);
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
            if (LibDbus.dbus_message_iter_open_container(iterPtr, LibDbus.DBUS_TYPE_ARRAY, sig.Pointer, &child) == 0)
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
            LibDbus.dbus_message_iter_close_container(iterPtr, &child);
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
            if (LibDbus.dbus_message_iter_open_container(iterPtr, LibDbus.DBUS_TYPE_ARRAY, sig.Pointer, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open dictionary container.");
            }
        }

        foreach (var entry in DBusCollectionHelpers.EnumerateDictionaryEntries(dict))
        {
            DBusMessageIter entryIter;
            var childPtr = &child;
            if (LibDbus.dbus_message_iter_open_container(childPtr, LibDbus.DBUS_TYPE_DICT_ENTRY, null, &entryIter) == 0)
            {
                throw new InvalidOperationException("Failed to open dictionary entry container.");
            }

            AppendValue(ref entryIter, entry.Key ?? throw new InvalidOperationException("Dictionary contains null keys."));
            AppendValue(ref entryIter, entry.Value ?? throw new InvalidOperationException("Dictionary contains null values."));

            LibDbus.dbus_message_iter_close_container(childPtr, &entryIter);
        }

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_close_container(iterPtr, &child);
        }
    }

    private static void AppendStruct(ref DBusMessageIter iter, DBusStruct dbusStruct)
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iterPtr = &iter)
        {
            if (LibDbus.dbus_message_iter_open_container(iterPtr, LibDbus.DBUS_TYPE_STRUCT, null, &child) == 0)
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
            LibDbus.dbus_message_iter_close_container(iterPtr, &child);
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
            if (LibDbus.dbus_message_iter_open_container(iterPtr, LibDbus.DBUS_TYPE_VARIANT, sig.Pointer, &child) == 0)
            {
                throw new InvalidOperationException("Failed to open variant container.");
            }
        }

        AppendValue(ref child, variant.Value);

        fixed (DBusMessageIter* iterPtr = &iter)
        {
            LibDbus.dbus_message_iter_close_container(iterPtr, &child);
        }
    }

    private static readonly Dictionary<Type, MethodInfo?> s_structConverters = new();
    private static readonly object s_structConvertersLock = new();

    private static bool TryConvertToDbusStruct(object value, out DBusStruct dbusStruct)
    {
        if (value is DBusStruct structValue)
        {
            dbusStruct = structValue;
            return true;
        }

        var type = value.GetType();
        MethodInfo? method;
        lock (s_structConvertersLock)
        {
            if (!s_structConverters.TryGetValue(type, out method))
            {
                method = type.GetMethod("ToDbusStruct", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                if (method is not null && method.ReturnType != typeof(DBusStruct))
                {
                    method = null;
                }

                s_structConverters[type] = method;
            }
        }

        if (method is null)
        {
            dbusStruct = null!;
            return false;
        }

        dbusStruct = (DBusStruct)method.Invoke(value, null)!;
        return true;
    }

}
