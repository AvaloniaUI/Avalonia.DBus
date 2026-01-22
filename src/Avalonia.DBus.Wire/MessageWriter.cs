using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public unsafe struct MessageWriter : IDisposable
{
    private DBusMessage* _message;
    private DBusMessageIter _iter;
    private bool _initialized;
    private bool _detached;

    internal MessageWriter(DBusMessage* message)
    {
        _message = message;
        _iter = default;
        _initialized = false;
        _detached = false;
    }

    public void WriteMethodCallHeader(string destination, string path, string @interface, string member, string? signature = null)
    {
        DisposeMessageIfNeeded();
        using var dest = new Utf8String(destination);
        using var pathUtf8 = new Utf8String(path);
        using var iface = new Utf8String(@interface);
        using var memberUtf8 = new Utf8String(member);
        _message = dbus.dbus_message_new_method_call(dest.Pointer, pathUtf8.Pointer, iface.Pointer, memberUtf8.Pointer);
        if (_message == null)
        {
            throw new InvalidOperationException("Failed to create method call message.");
        }
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_init_append(_message, iter);
        }
        _initialized = true;
    }

    public void WriteSignalHeader(string? destination, string path, string @interface, string member, string? signature = null)
    {
        DisposeMessageIfNeeded();
        using var pathUtf8 = new Utf8String(path);
        using var iface = new Utf8String(@interface);
        using var memberUtf8 = new Utf8String(member);
        _message = dbus.dbus_message_new_signal(pathUtf8.Pointer, iface.Pointer, memberUtf8.Pointer);
        if (_message == null)
        {
            throw new InvalidOperationException("Failed to create signal message.");
        }
        if (!string.IsNullOrEmpty(destination))
        {
            using var dest = new Utf8String(destination);
            dbus.dbus_message_set_destination(_message, dest.Pointer);
        }
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_init_append(_message, iter);
        }
        _initialized = true;
    }

    internal static MessageWriter CreateReply(DBusMessage* request)
    {
        var reply = dbus.dbus_message_new_method_return(request);
        if (reply == null)
        {
            throw new InvalidOperationException("Failed to create reply message.");
        }
        var writer = new MessageWriter(reply);
        DBusMessageIter iter = writer._iter;
        dbus.dbus_message_iter_init_append(reply, &iter);
        writer._iter = iter;
        writer._initialized = true;
        return writer;
    }

    public MessageBuffer CreateMessage()
    {
        if (_message == null)
        {
            throw new InvalidOperationException("MessageWriter has no message.");
        }
        _detached = true;
        return new MessageBuffer(_message);
    }

    public void Dispose()
    {
        if (_detached)
        {
            _message = null;
            _initialized = false;
            _detached = false;
            return;
        }
        DisposeMessageIfNeeded();
    }

    private void DisposeMessageIfNeeded()
    {
        if (_message != null)
        {
            dbus.dbus_message_unref(_message);
            _message = null;
            _initialized = false;
            _detached = false;
        }
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("MessageWriter is not initialized.");
        }
    }

    public void WriteByte(byte value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_BYTE, &value);
        }
    }

    public void WriteBool(bool value)
    {
        EnsureInitialized();
        uint dbusValue = value ? 1u : 0u;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_BOOLEAN, &dbusValue);
        }
    }

    public void WriteInt16(short value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_INT16, &value);
        }
    }

    public void WriteUInt16(ushort value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_UINT16, &value);
        }
    }

    public void WriteInt32(int value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_INT32, &value);
        }
    }

    public void WriteUInt32(uint value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_UINT32, &value);
        }
    }

    public void WriteInt64(long value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_INT64, &value);
        }
    }

    public void WriteUInt64(ulong value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_UINT64, &value);
        }
    }

    public void WriteDouble(double value)
    {
        EnsureInitialized();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_DOUBLE, &value);
        }
    }

    public void WriteString(string value)
    {
        EnsureInitialized();
        using var utf8 = new Utf8String(value);
        var ptr = utf8.Pointer;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_STRING, &ptr);
        }
    }

    public void WriteObjectPath(string value)
    {
        EnsureInitialized();
        using var utf8 = new Utf8String(value);
        var ptr = utf8.Pointer;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_OBJECT_PATH, &ptr);
        }
    }

    public void WriteObjectPath(ObjectPath value) => WriteObjectPath(value.ToString());

    public void WriteSignature(string value)
    {
        EnsureInitialized();
        using var utf8 = new Utf8String(value);
        var ptr = utf8.Pointer;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_SIGNATURE, &ptr);
        }
    }

    public void WriteSignature(Signature value) => WriteSignature(value.ToString());

    public void WriteHandle(SafeHandle value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        EnsureInitialized();
        int fd = value.DangerousGetHandle().ToInt32();
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_append_basic(iter, dbus.DBUS_TYPE_UNIX_FD, &fd);
        }
    }

    public ArrayStart WriteArrayStart(string elementSignature)
    {
        EnsureInitialized();
        var parent = _iter;
        DBusMessageIter child;
        using var sig = new Utf8String(elementSignature);
        if (dbus.dbus_message_iter_open_container(&parent, dbus.DBUS_TYPE_ARRAY, sig.Pointer, &child) == 0)
        {
            throw new InvalidOperationException("Failed to open array container.");
        }
        _iter = child;
        return new ArrayStart(parent);
    }

    public ArrayStart WriteArrayStart(DBusType elementType)
        => WriteArrayStart(((char)elementType).ToString());

    public void WriteArrayEnd(ArrayStart start)
    {
        EnsureInitialized();
        var parent = start.Parent;
        var child = _iter;
        if (dbus.dbus_message_iter_close_container(&parent, &child) == 0)
        {
            throw new InvalidOperationException("Failed to close array container.");
        }
        _iter = parent;
    }

    public DictionaryStart WriteDictionaryStart(string entrySignature)
    {
        var arrayStart = WriteArrayStart(entrySignature);
        return new DictionaryStart(arrayStart.Parent);
    }

    public void WriteDictionaryEnd(DictionaryStart start)
    {
        WriteArrayEnd(new ArrayStart(start.Parent));
    }

    public DictEntryStart WriteDictEntryStart()
    {
        EnsureInitialized();
        var parent = _iter;
        DBusMessageIter child;
        if (dbus.dbus_message_iter_open_container(&parent, dbus.DBUS_TYPE_DICT_ENTRY, null, &child) == 0)
        {
            throw new InvalidOperationException("Failed to open dict entry container.");
        }
        _iter = child;
        return new DictEntryStart(parent);
    }

    public void WriteDictEntryEnd(DictEntryStart start)
    {
        EnsureInitialized();
        var parent = start.Parent;
        var child = _iter;
        if (dbus.dbus_message_iter_close_container(&parent, &child) == 0)
        {
            throw new InvalidOperationException("Failed to close dict entry container.");
        }
        _iter = parent;
    }

    public StructStart WriteStructStart()
    {
        EnsureInitialized();
        var parent = _iter;
        DBusMessageIter child;
        if (dbus.dbus_message_iter_open_container(&parent, dbus.DBUS_TYPE_STRUCT, null, &child) == 0)
        {
            throw new InvalidOperationException("Failed to open struct container.");
        }
        _iter = child;
        return new StructStart(parent);
    }

    public void WriteStructEnd(StructStart start)
    {
        EnsureInitialized();
        var parent = start.Parent;
        var child = _iter;
        if (dbus.dbus_message_iter_close_container(&parent, &child) == 0)
        {
            throw new InvalidOperationException("Failed to close struct container.");
        }
        _iter = parent;
    }

    public VariantStart WriteVariantStart(string signature)
    {
        EnsureInitialized();
        var parent = _iter;
        DBusMessageIter child;
        using var sig = new Utf8String(signature);
        if (dbus.dbus_message_iter_open_container(&parent, dbus.DBUS_TYPE_VARIANT, sig.Pointer, &child) == 0)
        {
            throw new InvalidOperationException("Failed to open variant container.");
        }
        _iter = child;
        return new VariantStart(parent);
    }

    public void WriteVariantEnd(VariantStart start)
    {
        EnsureInitialized();
        var parent = start.Parent;
        var child = _iter;
        if (dbus.dbus_message_iter_close_container(&parent, &child) == 0)
        {
            throw new InvalidOperationException("Failed to close variant container.");
        }
        _iter = parent;
    }

    public void WriteVariant(VariantValue value)
    {
        if (string.IsNullOrEmpty(value.Signature))
        {
            throw new ArgumentException("VariantValue must have a signature.", nameof(value));
        }
        var variant = WriteVariantStart(value.Signature);
        WriteVariantValue(value.Signature, value.Value);
        WriteVariantEnd(variant);
    }

    private void WriteVariantValue(string signature, object? value)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return;
        }

        int index = 0;
        WriteSignatureValue(signature, ref index, value);
        if (index != signature.Length)
        {
            throw new ArgumentException("Variant signature must describe a single complete type.", nameof(signature));
        }
    }

    private void WriteSignatureValue(string signature, ref int index, object? value)
    {
        if (index >= signature.Length)
        {
            throw new ArgumentException("Variant signature is empty.", nameof(signature));
        }

        char token = signature[index];
        switch (token)
        {
            case 'y':
                index++;
                WriteByte(Convert.ToByte(value));
                return;
            case 'b':
                index++;
                WriteBool(Convert.ToBoolean(value));
                return;
            case 'n':
                index++;
                WriteInt16(Convert.ToInt16(value));
                return;
            case 'q':
                index++;
                WriteUInt16(Convert.ToUInt16(value));
                return;
            case 'i':
                index++;
                WriteInt32(Convert.ToInt32(value));
                return;
            case 'u':
                index++;
                WriteUInt32(Convert.ToUInt32(value));
                return;
            case 'x':
                index++;
                WriteInt64(Convert.ToInt64(value));
                return;
            case 't':
                index++;
                WriteUInt64(Convert.ToUInt64(value));
                return;
            case 'd':
                index++;
                WriteDouble(Convert.ToDouble(value));
                return;
            case 's':
                index++;
                WriteString(value?.ToString() ?? string.Empty);
                return;
            case 'o':
                index++;
                if (value is ObjectPath objectPath)
                {
                    WriteObjectPath(objectPath);
                }
                else
                {
                    WriteObjectPath(value?.ToString() ?? string.Empty);
                }
                return;
            case 'g':
                index++;
                if (value is Signature signatureValue)
                {
                    WriteSignature(signatureValue);
                }
                else
                {
                    WriteSignature(value?.ToString() ?? string.Empty);
                }
                return;
            case 'v':
                index++;
                WriteVariant(AsVariantValue(value));
                return;
            case 'a':
                index++;
                string elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                if (elementSignature.Length > 0 && elementSignature[0] == '{')
                {
                    WriteDictionaryValue(elementSignature, value);
                }
                else
                {
                    WriteArrayValue(elementSignature, value);
                }
                return;
            case '(':
                string structSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                WriteStructValue(structSignature, value);
                return;
            case '{':
                string entrySignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                WriteDictEntryValue(entrySignature, value);
                return;
            default:
                throw new NotSupportedException($"Variant signature '{signature}' is not supported.");
        }
    }

    private void WriteArrayValue(string elementSignature, object? value)
    {
        var arrayStart = WriteArrayStart(elementSignature);
        foreach (object? item in EnumerateValues(value))
        {
            int index = 0;
            WriteSignatureValue(elementSignature, ref index, item);
        }
        WriteArrayEnd(arrayStart);
    }

    private void WriteDictionaryValue(string entrySignature, object? value)
    {
        var dictStart = WriteDictionaryStart(entrySignature);
        (string keySignature, string valueSignature) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        foreach ((object? Key, object? Value) entry in EnumerateDictionaryEntries(value))
        {
            var entryStart = WriteDictEntryStart();
            int keyIndex = 0;
            int valueIndex = 0;
            WriteSignatureValue(keySignature, ref keyIndex, entry.Key);
            WriteSignatureValue(valueSignature, ref valueIndex, entry.Value);
            WriteDictEntryEnd(entryStart);
        }
        WriteDictionaryEnd(dictStart);
    }

    private void WriteStructValue(string structSignature, object? value)
    {
        IReadOnlyList<string> parts = DBusSignatureParser.ParseStructSignatures(structSignature);
        object?[] items = GetTupleItems(value);
        if (items.Length != parts.Count)
        {
            throw new ArgumentException("Struct value length does not match signature.", nameof(value));
        }

        var structStart = WriteStructStart();
        for (int i = 0; i < parts.Count; i++)
        {
            int index = 0;
            WriteSignatureValue(parts[i], ref index, items[i]);
        }
        WriteStructEnd(structStart);
    }

    private void WriteDictEntryValue(string entrySignature, object? value)
    {
        (string keySignature, string valueSignature) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        (object? Key, object? Value) entry = GetEntryValue(value);
        var entryStart = WriteDictEntryStart();
        int keyIndex = 0;
        int valueIndex = 0;
        WriteSignatureValue(keySignature, ref keyIndex, entry.Key);
        WriteSignatureValue(valueSignature, ref valueIndex, entry.Value);
        WriteDictEntryEnd(entryStart);
    }

    private static VariantValue AsVariantValue(object? value)
    {
        if (value is VariantValue variant)
        {
            return variant;
        }

        return value switch
        {
            byte v => v,
            bool v => v,
            short v => v,
            ushort v => v,
            int v => v,
            uint v => v,
            long v => v,
            ulong v => v,
            double v => v,
            string v => v,
            ObjectPath v => v,
            Signature v => v,
            _ => throw new ArgumentException("Variant values must be VariantValue or a supported primitive.", nameof(value))
        };
    }

    private static IEnumerable<object?> EnumerateValues(object? value)
    {
        if (value == null)
        {
            yield break;
        }

        if (value is string)
        {
            throw new ArgumentException("Array values must not be a string.", nameof(value));
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return item;
            }
            yield break;
        }

        throw new ArgumentException("Array values must be enumerable.", nameof(value));
    }

    private static IEnumerable<(object? Key, object? Value)> EnumerateDictionaryEntries(object? value)
    {
        if (value == null)
        {
            yield break;
        }

        if (value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                yield return (entry.Key, entry.Value);
            }
            yield break;
        }

        if (value is string)
        {
            throw new ArgumentException("Dictionary values must not be a string.", nameof(value));
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object? item in enumerable)
            {
                yield return GetEntryValue(item);
            }
            yield break;
        }

        throw new ArgumentException("Dictionary values must be enumerable.", nameof(value));
    }

    private static (object? Key, object? Value) GetEntryValue(object? item)
    {
        if (item is DictionaryEntry entry)
        {
            return (entry.Key, entry.Value);
        }

        if (item is ITuple tuple)
        {
            if (tuple.Length != 2)
            {
                throw new ArgumentException("Dictionary entry tuple must have two elements.", nameof(item));
            }
            return (tuple[0], tuple[1]);
        }

        if (item is IList list)
        {
            if (list.Count < 2)
            {
                throw new ArgumentException("Dictionary entry list must have at least two elements.", nameof(item));
            }
            return (list[0], list[1]);
        }

        if (TryGetKeyValuePair(item, out object? key, out object? value))
        {
            return (key, value);
        }

        throw new ArgumentException("Dictionary entries must be key/value pairs.", nameof(item));
    }

    private static bool TryGetKeyValuePair(object? item, out object? key, out object? value)
    {
        key = null;
        value = null;
        if (item == null)
        {
            return false;
        }

        Type type = item.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
        {
            return false;
        }

        key = type.GetProperty("Key")?.GetValue(item);
        value = type.GetProperty("Value")?.GetValue(item);
        return true;
    }

    private static object?[] GetTupleItems(object? value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value is ITuple tuple)
        {
            object?[] items = new object?[tuple.Length];
            for (int i = 0; i < tuple.Length; i++)
            {
                items[i] = tuple[i];
            }
            return items;
        }

        if (value is string)
        {
            throw new ArgumentException("Struct values must not be a string.", nameof(value));
        }

        if (value is IEnumerable enumerable)
        {
            List<object?> items = new();
            foreach (object? item in enumerable)
            {
                items.Add(item);
            }
            return items.ToArray();
        }

        throw new ArgumentException("Struct values must be tuple-like.", nameof(value));
    }
}

public sealed class ArrayStart
{
    internal ArrayStart(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class DictionaryStart
{
    internal DictionaryStart(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class DictEntryStart
{
    internal DictEntryStart(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class StructStart
{
    internal StructStart(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class VariantStart
{
    internal VariantStart(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}
