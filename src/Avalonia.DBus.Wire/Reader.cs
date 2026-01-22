using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.DBus.AutoGen;
using Microsoft.Win32.SafeHandles;

namespace Avalonia.DBus.Wire;

public unsafe struct Reader
{
    private DBusMessageIter _iter;

    private Reader(DBusMessageIter iter)
    {
        _iter = iter;
    }

    internal static Reader Create(DBusMessage* message)
    {
        DBusMessageIter iter;
        if (dbus.dbus_message_iter_init(message, &iter) == 0)
        {
            iter = default;
        }
        return new Reader(iter);
    }

    public byte ReadByte()
    {
        byte value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public bool ReadBool()
    {
        uint value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value != 0;
    }

    public short ReadInt16()
    {
        short value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public ushort ReadUInt16()
    {
        ushort value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public int ReadInt32()
    {
        int value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public uint ReadUInt32()
    {
        uint value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public long ReadInt64()
    {
        long value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public ulong ReadUInt64()
    {
        ulong value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public double ReadDouble()
    {
        double value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return value;
    }

    public string ReadString()
    {
        byte* value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return DbusHelpers.PtrToString(value);
    }

    public string ReadObjectPath() => ReadString();

    public string ReadSignature()
    {
        byte* value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        return DbusHelpers.PtrToString(value);
    }

    public T ReadHandle<T>() where T : SafeHandle
    {
        int value;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_get_basic(iter, &value);
            dbus.dbus_message_iter_next(iter);
        }
        var handle = new SafeFileHandle(new IntPtr(value), ownsHandle: true);
        return (T)(object)handle;
    }

    public ArrayEnd ReadArrayStart() => ReadArrayStartInternal();

    public ArrayEnd ReadArrayStart(DBusType elementType) => ReadArrayStartInternal();

    private ArrayEnd ReadArrayStartInternal()
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_recurse(iter, &child);
        }
        var end = new ArrayEnd(_iter);
        _iter = child;
        return end;
    }

    public bool HasNext(ArrayEnd end)
    {
        if (end.Done)
        {
            return false;
        }

        if (!end.Started)
        {
            end.Started = true;
        }

        int argType;
        fixed (DBusMessageIter* iter = &_iter)
        {
            argType = dbus.dbus_message_iter_get_arg_type(iter);
        }

        if (argType == dbus.DBUS_TYPE_INVALID)
        {
            end.Done = true;
            _iter = end.Parent;
            fixed (DBusMessageIter* iter = &_iter)
            {
                dbus.dbus_message_iter_next(iter);
            }
            return false;
        }

        return true;
    }

    public StructEnd ReadStructStart() => ReadStructStartInternal();

    public DictEntryEnd ReadDictEntryStart() => new DictEntryEnd(ReadStructStartInternal().Parent);

    private StructEnd ReadStructStartInternal()
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_recurse(iter, &child);
        }
        var end = new StructEnd(_iter);
        _iter = child;
        return end;
    }

    public void ReadStructEnd(StructEnd end)
    {
        _iter = end.Parent;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_next(iter);
        }
    }

    public void ReadDictEntryEnd(DictEntryEnd end)
    {
        _iter = end.Parent;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_next(iter);
        }
    }

    public VariantEnd ReadVariantStart()
    {
        DBusMessageIter child;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_recurse(iter, &child);
        }
        var end = new VariantEnd(_iter);
        _iter = child;
        return end;
    }

    public void ReadVariantEnd(VariantEnd end)
    {
        _iter = end.Parent;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_next(iter);
        }
    }

    public VariantValue ReadVariantValue()
    {
        byte* signaturePtr;
        fixed (DBusMessageIter* iter = &_iter)
        {
            signaturePtr = dbus.dbus_message_iter_get_signature(iter);
        }
        string signature = signaturePtr == null ? string.Empty : DbusHelpers.PtrToString(signaturePtr);
        if (signaturePtr != null)
        {
            NativeMethods.dbus_free(signaturePtr);
        }

        DBusMessageIter child;
        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_recurse(iter, &child);
        }
        var variantReader = new Reader(child);
        object? value = ReadVariantValueInternal(signature, ref variantReader);

        fixed (DBusMessageIter* iter = &_iter)
        {
            dbus.dbus_message_iter_next(iter);
        }
        return new VariantValue(signature, value);
    }

    private static object? ReadVariantValueInternal(string signature, ref Reader reader)
    {
        if (string.IsNullOrEmpty(signature))
        {
            SkipCurrent(ref reader);
            return null;
        }

        int index = 0;
        return ReadSignatureValue(signature, ref index, ref reader);
    }

    private static object? ReadSignatureValue(string signature, ref int index, ref Reader reader)
    {
        if (index >= signature.Length)
        {
            return null;
        }

        char token = signature[index];
        switch (token)
        {
            case 'y':
                index++;
                return reader.ReadByte();
            case 'b':
                index++;
                return reader.ReadBool();
            case 'n':
                index++;
                return reader.ReadInt16();
            case 'q':
                index++;
                return reader.ReadUInt16();
            case 'i':
                index++;
                return reader.ReadInt32();
            case 'u':
                index++;
                return reader.ReadUInt32();
            case 'x':
                index++;
                return reader.ReadInt64();
            case 't':
                index++;
                return reader.ReadUInt64();
            case 'd':
                index++;
                return reader.ReadDouble();
            case 's':
                index++;
                return reader.ReadString();
            case 'o':
                index++;
                return reader.ReadObjectPath();
            case 'g':
                index++;
                return reader.ReadSignature();
            case 'v':
                index++;
                return reader.ReadVariantValue();
            case 'a':
                index++;
                string elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                if (elementSignature.Length > 0 && elementSignature[0] == '{')
                {
                    return ReadDictionaryValue(elementSignature, ref reader);
                }
                return ReadArrayValue(elementSignature, ref reader);
            case '(':
                string structSignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                return ReadStructValue(structSignature, ref reader);
            case '{':
                string entrySignature = DBusSignatureParser.ReadSingleType(signature, ref index);
                return ReadDictEntryValue(entrySignature, ref reader);
            default:
                SkipCurrent(ref reader);
                return null;
        }
    }

    private static List<object?> ReadArrayValue(string elementSignature, ref Reader reader)
    {
        ArrayEnd end = reader.ReadArrayStart();
        List<object?> items = new();
        while (reader.HasNext(end))
        {
            int elementIndex = 0;
            items.Add(ReadSignatureValue(elementSignature, ref elementIndex, ref reader));
        }
        return items;
    }

    private static List<KeyValuePair<object?, object?>> ReadDictionaryValue(string entrySignature, ref Reader reader)
    {
        (string keySignature, string valueSignature) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        ArrayEnd end = reader.ReadArrayStart();
        List<KeyValuePair<object?, object?>> items = new();
        while (reader.HasNext(end))
        {
            DictEntryEnd entryEnd = reader.ReadDictEntryStart();
            int keyIndex = 0;
            int valueIndex = 0;
            object? key = ReadSignatureValue(keySignature, ref keyIndex, ref reader);
            object? value = ReadSignatureValue(valueSignature, ref valueIndex, ref reader);
            reader.ReadDictEntryEnd(entryEnd);
            items.Add(new KeyValuePair<object?, object?>(key, value));
        }
        return items;
    }

    private static object?[] ReadStructValue(string structSignature, ref Reader reader)
    {
        IReadOnlyList<string> parts = DBusSignatureParser.ParseStructSignatures(structSignature);
        StructEnd end = reader.ReadStructStart();
        object?[] items = new object?[parts.Count];
        for (int i = 0; i < parts.Count; i++)
        {
            int elementIndex = 0;
            items[i] = ReadSignatureValue(parts[i], ref elementIndex, ref reader);
        }
        reader.ReadStructEnd(end);
        return items;
    }

    private static KeyValuePair<object?, object?> ReadDictEntryValue(string entrySignature, ref Reader reader)
    {
        (string keySignature, string valueSignature) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);
        DictEntryEnd entryEnd = reader.ReadDictEntryStart();
        int keyIndex = 0;
        int valueIndex = 0;
        object? key = ReadSignatureValue(keySignature, ref keyIndex, ref reader);
        object? value = ReadSignatureValue(valueSignature, ref valueIndex, ref reader);
        reader.ReadDictEntryEnd(entryEnd);
        return new KeyValuePair<object?, object?>(key, value);
    }

    private static void SkipCurrent(ref Reader reader)
    {
        int argType;
        fixed (DBusMessageIter* iter = &reader._iter)
        {
            argType = dbus.dbus_message_iter_get_arg_type(iter);
        }
        if (argType == dbus.DBUS_TYPE_INVALID)
        {
            return;
        }

        if (argType == dbus.DBUS_TYPE_ARRAY || argType == dbus.DBUS_TYPE_STRUCT || argType == dbus.DBUS_TYPE_DICT_ENTRY || argType == dbus.DBUS_TYPE_VARIANT)
        {
            DBusMessageIter child;
            fixed (DBusMessageIter* iter = &reader._iter)
            {
                dbus.dbus_message_iter_recurse(iter, &child);
            }
            var childReader = new Reader(child);
            while (true)
            {
                int childType;
                DBusMessageIter childIter = childReader._iter;
                childType = dbus.dbus_message_iter_get_arg_type(&childIter);
                if (childType == dbus.DBUS_TYPE_INVALID)
                {
                    break;
                }
                SkipCurrent(ref childReader);
            }
            fixed (DBusMessageIter* iter = &reader._iter)
            {
                dbus.dbus_message_iter_next(iter);
            }
            return;
        }

        fixed (DBusMessageIter* iter = &reader._iter)
        {
            dbus.dbus_message_iter_next(iter);
        }
    }
}

public sealed class ArrayEnd
{
    internal ArrayEnd(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
    internal bool Started { get; set; }
    internal bool Done { get; set; }
}

public sealed class StructEnd
{
    internal StructEnd(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class DictEntryEnd
{
    internal DictEntryEnd(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}

public sealed class VariantEnd
{
    internal VariantEnd(DBusMessageIter parent)
    {
        Parent = parent;
    }

    internal DBusMessageIter Parent { get; }
}
