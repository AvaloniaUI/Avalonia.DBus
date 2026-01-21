using System;
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
        if (signature.Length == 0)
        {
            return;
        }
        switch (signature[0])
        {
            case 'y':
                WriteByte(Convert.ToByte(value));
                return;
            case 'b':
                WriteBool(Convert.ToBoolean(value));
                return;
            case 'n':
                WriteInt16(Convert.ToInt16(value));
                return;
            case 'q':
                WriteUInt16(Convert.ToUInt16(value));
                return;
            case 'i':
                WriteInt32(Convert.ToInt32(value));
                return;
            case 'u':
                WriteUInt32(Convert.ToUInt32(value));
                return;
            case 'x':
                WriteInt64(Convert.ToInt64(value));
                return;
            case 't':
                WriteUInt64(Convert.ToUInt64(value));
                return;
            case 'd':
                WriteDouble(Convert.ToDouble(value));
                return;
            case 's':
                WriteString(value?.ToString() ?? string.Empty);
                return;
            case 'o':
                WriteObjectPath(value?.ToString() ?? string.Empty);
                return;
            case 'g':
                WriteSignature(value?.ToString() ?? string.Empty);
                return;
            default:
                throw new NotSupportedException($"Variant signature '{signature}' is not supported.");
        }
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
