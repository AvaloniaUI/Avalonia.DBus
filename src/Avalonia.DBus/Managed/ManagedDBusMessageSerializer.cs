using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.DBus.Managed;

/// <summary>
/// Converts between <see cref="DBusMessage"/> and <see cref="DBusSerializedMessage"/>
/// using the managed <see cref="DBusWireWriter"/> and <see cref="DBusWireReader"/>.
/// </summary>
internal sealed class ManagedDBusMessageSerializer : IDBusMessageSerializer
{
    // D-Bus header field codes
    private const byte FieldPath = 1;
    private const byte FieldInterface = 2;
    private const byte FieldMember = 3;
    private const byte FieldErrorName = 4;
    private const byte FieldReplySerial = 5;
    private const byte FieldDestination = 6;
    private const byte FieldSender = 7;
    private const byte FieldSignature = 8;
    private const byte FieldUnixFds = 9;

    public DBusSerializedMessage Serialize(DBusMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Step 1: Write body first to get body bytes and collect fds
        var fds = new List<int>();
        byte[] bodyBytes;

        if (message.Body.Count > 0)
        {
            var bodyWriter = new DBusWireWriter();

            foreach (var item in message.Body)
            {
                WriteValue(bodyWriter, item, fds);
            }

            bodyBytes = bodyWriter.ToArray();
        }
        else
        {
            bodyBytes = [];
        }

        var bodyLength = (uint)bodyBytes.Length;

        // Step 2: Write header
        var headerWriter = new DBusWireWriter();

        // Fixed header (12 bytes)
        headerWriter.WriteByte((byte)'l'); // little-endian
        headerWriter.WriteByte((byte)message.Type);
        headerWriter.WriteByte((byte)message.Flags);
        headerWriter.WriteByte(1); // protocol version

        headerWriter.WriteUInt32(bodyLength);
        headerWriter.WriteUInt32(message.Serial);

        // Header fields array
        // Write placeholder for array length
        var arrayLengthPos = headerWriter.Position;
        headerWriter.WriteUInt32(0); // placeholder

        var arrayStartPos = headerWriter.Position;

        // Write header fields
        if (message.Path.HasValue)
        {
            WriteHeaderField(headerWriter, FieldPath, "o", writer => writer.WriteObjectPath(message.Path.Value.Value));
        }

        if (!string.IsNullOrEmpty(message.Interface))
        {
            WriteHeaderField(headerWriter, FieldInterface, "s", writer => writer.WriteString(message.Interface));
        }

        if (!string.IsNullOrEmpty(message.Member))
        {
            WriteHeaderField(headerWriter, FieldMember, "s", writer => writer.WriteString(message.Member));
        }

        if (!string.IsNullOrEmpty(message.ErrorName))
        {
            WriteHeaderField(headerWriter, FieldErrorName, "s", writer => writer.WriteString(message.ErrorName));
        }

        if (message.ReplySerial != 0)
        {
            WriteHeaderField(headerWriter, FieldReplySerial, "u", writer => writer.WriteUInt32(message.ReplySerial));
        }

        if (!string.IsNullOrEmpty(message.Destination))
        {
            WriteHeaderField(headerWriter, FieldDestination, "s", writer => writer.WriteString(message.Destination));
        }

        if (!string.IsNullOrEmpty(message.Sender))
        {
            WriteHeaderField(headerWriter, FieldSender, "s", writer => writer.WriteString(message.Sender));
        }

        if (!string.IsNullOrEmpty(message.Signature.Value))
        {
            WriteHeaderField(headerWriter, FieldSignature, "g", writer => writer.WriteSignature(message.Signature.Value));
        }

        if (fds.Count > 0)
        {
            WriteHeaderField(headerWriter, FieldUnixFds, "u", writer => writer.WriteUInt32((uint)fds.Count));
        }

        var arrayEndPos = headerWriter.Position;

        // Backpatch array length
        var arrayLength = CheckedLength(arrayEndPos - arrayStartPos, "Header fields array");
        headerWriter.SetPosition(arrayLengthPos);
        headerWriter.WriteUInt32(arrayLength);
        headerWriter.SetPosition(arrayEndPos);

        // Pad to 8-byte boundary after header fields
        headerWriter.WritePad(8);

        // Step 3: Combine header and body
        var headerBytes = headerWriter.ToArray();
        var fullMessageLength = (long)headerBytes.Length + bodyBytes.Length;
        if (fullMessageLength > int.MaxValue)
        {
            throw new InvalidOperationException($"Serialized message is too large ({fullMessageLength} bytes).");
        }

        var fullMessage = new byte[(int)fullMessageLength];
        Array.Copy(headerBytes, 0, fullMessage, 0, headerBytes.Length);
        Array.Copy(bodyBytes, 0, fullMessage, headerBytes.Length, bodyBytes.Length);

        return new DBusSerializedMessage(fullMessage, fds.ToArray());
    }

    public DBusMessage Deserialize(DBusSerializedMessage serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        if (serialized.Message.Length < 16)
        {
            throw new InvalidDataException(
                $"D-Bus message is too short ({serialized.Message.Length} bytes). Minimum header size is 16 bytes.");
        }

        // Peek at endianness byte to create the reader with the correct byte order
        var endiannessByte = serialized.Message[0];
        var bigEndian = endiannessByte switch
        {
            (byte)'l' => false,
            (byte)'B' => true,
            _ => throw new NotSupportedException(
                $"Invalid endianness byte 0x{endiannessByte:X2}. Expected 'l' (0x6C) or 'B' (0x42).")
        };

        var reader = new DBusWireReader(serialized.Message, bigEndian);

        // Read fixed header (bytes 0-3)
        reader.ReadByte(); // consume endianness byte (already parsed above)

        var type = (DBusMessageType)reader.ReadByte();
        var flags = (DBusMessageFlags)reader.ReadByte();
        var protocolVersion = reader.ReadByte();

        if (protocolVersion != 1)
        {
            throw new NotSupportedException($"Unsupported protocol version: {protocolVersion}");
        }

        // Read body length and serial (bytes 4-11)
        var bodyLength = reader.ReadUInt32();
        var serial = reader.ReadUInt32();

        // Read header fields array
        var arrayByteLength = reader.ReadUInt32();
        var arrayEndPos = ComputeBoundedEndPosition(reader, arrayByteLength, "header fields array");

        DBusObjectPath? path = null;
        string? iface = null;
        string? member = null;
        string? errorName = null;
        uint replySerial = 0;
        string? destination = null;
        string? sender = null;
        var signature = string.Empty;

        while (reader.Position < arrayEndPos)
        {
            reader.ReadPad(8); // each struct in the array is 8-byte aligned

            if (reader.Position >= arrayEndPos)
                break;

            var fieldCode = reader.ReadByte();
            var variantSig = reader.ReadSignature();

            switch (fieldCode)
            {
                case FieldPath:
                    path = new DBusObjectPath(reader.ReadString());
                    break;
                case FieldInterface:
                    iface = reader.ReadString();
                    break;
                case FieldMember:
                    member = reader.ReadString();
                    break;
                case FieldErrorName:
                    errorName = reader.ReadString();
                    break;
                case FieldReplySerial:
                    replySerial = reader.ReadUInt32();
                    break;
                case FieldDestination:
                    destination = reader.ReadString();
                    break;
                case FieldSender:
                    sender = reader.ReadString();
                    break;
                case FieldSignature:
                    signature = reader.ReadSignature();
                    break;
                case FieldUnixFds:
                    reader.ReadUInt32(); // we get the fds from the serialized message
                    break;
                default:
                    // Skip unknown field by reading the variant value according to its signature
                    SkipValue(reader, variantSig);
                    break;
            }
        }

        // Ensure we're at the end of the header fields array
        reader.Position = arrayEndPos;

        // Pad to 8-byte boundary after header fields
        reader.ReadPad(8);
        var bodyStart = reader.Position;
        var bodyEnd = ComputeBoundedEndPosition(reader, bodyLength, "message body");

        // Read body
        var body = new List<object>();
        if (!string.IsNullOrEmpty(signature))
        {
            var sigIndex = 0;
            while (sigIndex < signature.Length)
            {
                var typeSig = DBusSignatureParser.ReadSingleType(signature, ref sigIndex);
                body.Add(ReadValue(reader, typeSig, serialized.Fds));
            }
        }

        if (reader.Position != bodyEnd)
        {
            throw new InvalidDataException(
                $"Body parse consumed {reader.Position - bodyStart} bytes, but header declared {bodyLength} bytes.");
        }

        if (bodyEnd != reader.Length)
        {
            throw new InvalidDataException(
                $"Message contains trailing bytes after declared body (body end={bodyEnd}, total={reader.Length}).");
        }

        var msg = new DBusMessage
        {
            Type = type,
            Flags = flags,
            Serial = serial,
            ReplySerial = replySerial,
            Path = path,
            Interface = iface,
            Member = member,
            ErrorName = errorName,
            Destination = destination,
        };

        msg.Sender = sender;
        msg.SetBodyWithSignature(body, signature);

        return msg;
    }

    private static void WriteHeaderField(DBusWireWriter writer, byte fieldCode, string valueSig, Action<DBusWireWriter> writeValue)
    {
        writer.WritePad(8); // struct alignment
        writer.WriteByte(fieldCode);
        writer.WriteSignature(valueSig);
        writeValue(writer);
    }

    // --- Write value dispatch ---

    private static void WriteValue(DBusWireWriter writer, object value, List<int> fds)
    {
        ArgumentNullException.ThrowIfNull(value);

        switch (value)
        {
            case byte byteValue:
                writer.WriteByte(byteValue);
                return;
            case bool boolValue:
                writer.WriteBoolean(boolValue);
                return;
            case short int16Value:
                writer.WriteInt16(int16Value);
                return;
            case ushort uint16Value:
                writer.WriteUInt16(uint16Value);
                return;
            case int int32Value:
                writer.WriteInt32(int32Value);
                return;
            case uint uint32Value:
                writer.WriteUInt32(uint32Value);
                return;
            case long int64Value:
                writer.WriteInt64(int64Value);
                return;
            case ulong uint64Value:
                writer.WriteUInt64(uint64Value);
                return;
            case double doubleValue:
                writer.WriteDouble(doubleValue);
                return;
            case string stringValue:
                writer.WriteString(stringValue);
                return;
            case DBusObjectPath objectPath:
                writer.WriteObjectPath(objectPath.Value);
                return;
            case DBusSignature signature:
                writer.WriteSignature(signature.Value);
                return;
            case DBusUnixFd unixFd:
                fds.Add(unixFd.Fd);
                writer.WriteUInt32((uint)(fds.Count - 1));
                return;
            case DBusVariant variant:
                WriteVariant(writer, variant, fds);
                return;
            case DBusStruct dbusStruct:
                WriteStruct(writer, dbusStruct, fds);
                return;
            case IDBusStructConvertible structConvertible:
                WriteStruct(writer, structConvertible.ToDbusStruct(), fds);
                return;
            default:
                if (DBusCollectionHelpers.TryGetDictionaryTypes(value.GetType(), out _, out _) || value is IDictionary)
                {
                    WriteDict(writer, value, fds);
                    return;
                }

                if (DBusCollectionHelpers.TryGetListElementType(value.GetType(), out _) || value is IList)
                {
                    WriteArray(writer, value, fds);
                    return;
                }

                throw new NotSupportedException($"Unsupported D-Bus value type: {value.GetType().FullName}");
        }
    }

    private static void WriteVariant(DBusWireWriter writer, DBusVariant variant, List<int> fds)
    {
        var signature = variant.Signature.Value;
        if (string.IsNullOrEmpty(signature))
        {
            signature = DBusSignatureInference.InferSignatureFromValue(variant.Value);
        }

        writer.WriteSignature(signature);
        WriteValue(writer, variant.Value, fds);
    }

    private static void WriteStruct(DBusWireWriter writer, DBusStruct dbusStruct, List<int> fds)
    {
        writer.WritePad(8);
        foreach (var field in dbusStruct)
        {
            WriteValue(writer, field, fds);
        }
    }

    private static void WriteArray(DBusWireWriter writer, object array, List<int> fds)
    {
        var arraySignature = DBusSignatureInference.InferSignatureFromValue(array);
        if (arraySignature.Length < 2 || arraySignature[0] != DBusSignatureToken.Array)
        {
            throw new InvalidOperationException("Invalid array signature.");
        }

        var elementSignature = arraySignature.Substring(1);

        // Write uint32 placeholder for array byte length
        var lengthPos = writer.Position;
        writer.WriteUInt32(0); // placeholder

        // Pad to element alignment
        var elementAlignment = GetAlignmentForSignature(elementSignature);
        writer.WritePad(elementAlignment);

        var dataStartPos = writer.Position;

        foreach (var item in DBusCollectionHelpers.EnumerateListItems(array))
        {
            WriteValue(writer, item ?? throw new InvalidOperationException("Array contains null values."), fds);
        }

        var dataEndPos = writer.Position;

        // Backpatch array byte length
        var arrayByteLength = CheckedLength(dataEndPos - dataStartPos, "Array");
        writer.SetPosition(lengthPos);
        writer.WriteUInt32(arrayByteLength);
        writer.SetPosition(dataEndPos);
    }

    private static void WriteDict(DBusWireWriter writer, object dict, List<int> fds)
    {
        var arraySignature = DBusSignatureInference.InferSignatureFromValue(dict);
        if (arraySignature.Length < 3
            || arraySignature[0] != DBusSignatureToken.Array
            || arraySignature[1] != DBusSignatureToken.DictEntryBegin)
        {
            throw new InvalidOperationException("Invalid dictionary signature.");
        }

        // Write uint32 placeholder for array byte length
        var lengthPos = writer.Position;
        writer.WriteUInt32(0); // placeholder

        // Dict entries are structs, so pad to 8-byte alignment
        writer.WritePad(8);

        var dataStartPos = writer.Position;

        foreach (var entry in DBusCollectionHelpers.EnumerateDictionaryEntries(dict))
        {
            writer.WritePad(8); // each dict entry is a struct
            WriteValue(writer, entry.Key ?? throw new InvalidOperationException("Dictionary contains null keys."), fds);
            WriteValue(writer, entry.Value ?? throw new InvalidOperationException("Dictionary contains null values."), fds);
        }

        var dataEndPos = writer.Position;

        // Backpatch array byte length
        var arrayByteLength = CheckedLength(dataEndPos - dataStartPos, "Dictionary array");
        writer.SetPosition(lengthPos);
        writer.WriteUInt32(arrayByteLength);
        writer.SetPosition(dataEndPos);
    }

    // --- Read value dispatch ---

    private static object ReadValue(DBusWireReader reader, string signature, int[] fds)
    {
        if (string.IsNullOrEmpty(signature))
        {
            throw new ArgumentException("Signature is required.", nameof(signature));
        }

        DBusSignatureToken token = signature[0];

        if (token == DBusSignatureToken.Byte)
            return reader.ReadByte();
        if (token == DBusSignatureToken.Boolean)
            return reader.ReadBoolean();
        if (token == DBusSignatureToken.Int16)
            return reader.ReadInt16();
        if (token == DBusSignatureToken.UInt16)
            return reader.ReadUInt16();
        if (token == DBusSignatureToken.Int32)
            return reader.ReadInt32();
        if (token == DBusSignatureToken.UInt32)
            return reader.ReadUInt32();
        if (token == DBusSignatureToken.Int64)
            return reader.ReadInt64();
        if (token == DBusSignatureToken.UInt64)
            return reader.ReadUInt64();
        if (token == DBusSignatureToken.Double)
            return reader.ReadDouble();
        if (token == DBusSignatureToken.String)
            return reader.ReadString();
        if (token == DBusSignatureToken.ObjectPath)
            return new DBusObjectPath(reader.ReadString());
        if (token == DBusSignatureToken.Signature)
            return new DBusSignature(reader.ReadSignature());
        if (token == DBusSignatureToken.UnixFd)
        {
            var fdIndex = reader.ReadUInt32();
            if (fdIndex >= (uint)fds.Length)
            {
                throw new InvalidDataException(
                    $"D-Bus message references unix fd index {fdIndex}, but only {fds.Length} fds were provided.");
            }

            return new DBusUnixFd(fds[(int)fdIndex]);
        }
        if (token == DBusSignatureToken.Variant)
            return ReadVariant(reader, fds);
        if (token == DBusSignatureToken.Array)
            return ReadArray(reader, signature, fds);
        if (token == DBusSignatureToken.StructBegin)
            return ReadStruct(reader, signature, fds);

        throw new NotSupportedException($"Unsupported signature token: {signature[0]}");
    }

    private static DBusVariant ReadVariant(DBusWireReader reader, int[] fds)
    {
        var sig = reader.ReadSignature();
        var value = ReadValue(reader, sig, fds);
        return new DBusVariant(value);
    }

    private static object ReadArray(DBusWireReader reader, string signature, int[] fds)
    {
        var index = 1;
        var elementSignature = DBusSignatureParser.ReadSingleType(signature, ref index);

        if (elementSignature.Length > 0 && elementSignature[0] == DBusSignatureToken.DictEntryBegin)
        {
            return ReadDictionaryArray(reader, elementSignature, fds);
        }

        return ReadArrayItems(reader, elementSignature, fds);
    }

    private static object ReadArrayItems(DBusWireReader reader, string elementSignature, int[] fds)
    {
        var arrayByteLength = reader.ReadUInt32();

        // Pad to element alignment
        var elementAlignment = GetAlignmentForSignature(elementSignature);
        reader.ReadPad(elementAlignment);

        var endPos = ComputeBoundedEndPosition(reader, arrayByteLength, "array");

        var items = new List<object>();
        while (reader.Position < endPos)
        {
            items.Add(ReadValue(reader, elementSignature, fds));
        }

        return CreateArrayInstance(elementSignature, items);
    }

    private static object ReadDictionaryArray(DBusWireReader reader, string entrySignature, int[] fds)
    {
        var arrayByteLength = reader.ReadUInt32();

        // Dict entries are structs, so pad to 8-byte alignment
        reader.ReadPad(8);

        var endPos = ComputeBoundedEndPosition(reader, arrayByteLength, "dictionary array");

        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);

        var entries = new List<KeyValuePair<object?, object?>>();
        while (reader.Position < endPos)
        {
            reader.ReadPad(8); // each dict entry is a struct
            var key = ReadValue(reader, keySig, fds);
            var value = ReadValue(reader, valueSig, fds);
            entries.Add(new KeyValuePair<object?, object?>(key, value));
        }

        return CreateDictInstance(entrySignature, entries);
    }

    private static DBusStruct ReadStruct(DBusWireReader reader, string signature, int[] fds)
    {
        reader.ReadPad(8);

        var partSignatures = DBusSignatureParser.ParseStructSignatures(signature);
        var values = new List<object>(partSignatures.Count);
        foreach (var part in partSignatures)
        {
            values.Add(ReadValue(reader, part, fds));
        }

        return new DBusStruct(values);
    }

    // --- Skip unknown variant values ---

    private static void SkipValue(DBusWireReader reader, string signature)
    {
        if (string.IsNullOrEmpty(signature))
            return;

        var index = 0;
        while (index < signature.Length)
        {
            var typeSig = DBusSignatureParser.ReadSingleType(signature, ref index);
            SkipSingleValue(reader, typeSig);
        }
    }

    private static void SkipSingleValue(DBusWireReader reader, string signature)
    {
        DBusSignatureToken token = signature[0];

        if (token == DBusSignatureToken.Byte)
        {
            reader.ReadByte();
        }
        else if (token == DBusSignatureToken.Boolean)
        {
            reader.ReadBoolean();
        }
        else if (token == DBusSignatureToken.Int16 || token == DBusSignatureToken.UInt16)
        {
            reader.ReadInt16();
        }
        else if (token == DBusSignatureToken.Int32 || token == DBusSignatureToken.UInt32)
        {
            reader.ReadInt32();
        }
        else if (token == DBusSignatureToken.Int64 || token == DBusSignatureToken.UInt64 ||
                 token == DBusSignatureToken.Double)
        {
            reader.ReadInt64();
        }
        else if (token == DBusSignatureToken.String || token == DBusSignatureToken.ObjectPath)
        {
            reader.ReadString();
        }
        else if (token == DBusSignatureToken.Signature)
        {
            reader.ReadSignature();
        }
        else if (token == DBusSignatureToken.UnixFd)
        {
            reader.ReadUInt32();
        }
        else if (token == DBusSignatureToken.Variant)
        {
            var varSig = reader.ReadSignature();
            SkipValue(reader, varSig);
        }
        else if (token == DBusSignatureToken.Array)
        {
            var arrayLen = reader.ReadUInt32();
            var idx = 1;
            var elemSig = DBusSignatureParser.ReadSingleType(signature, ref idx);
            var elemAlign = GetAlignmentForSignature(elemSig);
            reader.ReadPad(elemAlign);
            var endPos = ComputeBoundedEndPosition(reader, arrayLen, "array");
            reader.Position = endPos;
        }
        else if (token == DBusSignatureToken.StructBegin)
        {
            reader.ReadPad(8);
            var parts = DBusSignatureParser.ParseStructSignatures(signature);
            foreach (var part in parts)
                SkipSingleValue(reader, part);
        }
    }

    // --- Alignment helpers ---

    private static int GetAlignmentForSignature(string signature)
    {
        if (string.IsNullOrEmpty(signature))
            return 1;

        DBusSignatureToken token = signature[0];

        if (token == DBusSignatureToken.Byte)
            return 1;
        if (token == DBusSignatureToken.Boolean)
            return 4;
        if (token == DBusSignatureToken.Int16 || token == DBusSignatureToken.UInt16)
            return 2;
        if (token == DBusSignatureToken.Int32 || token == DBusSignatureToken.UInt32)
            return 4;
        if (token == DBusSignatureToken.Int64 || token == DBusSignatureToken.UInt64)
            return 8;
        if (token == DBusSignatureToken.Double)
            return 8;
        if (token == DBusSignatureToken.String || token == DBusSignatureToken.ObjectPath)
            return 4;
        if (token == DBusSignatureToken.Signature)
            return 1;
        if (token == DBusSignatureToken.UnixFd)
            return 4;
        if (token == DBusSignatureToken.Array)
            return 4;
        if (token == DBusSignatureToken.StructBegin || token == DBusSignatureToken.DictEntryBegin)
            return 8;
        if (token == DBusSignatureToken.Variant)
            return 1;

        return 1;
    }

    private static uint CheckedLength(long length, string sectionName)
    {
        if (length < 0 || length > uint.MaxValue)
        {
            throw new InvalidOperationException($"{sectionName} length {length} is outside uint32 range.");
        }

        return (uint)length;
    }

    private static int ComputeBoundedEndPosition(DBusWireReader reader, uint byteLength, string sectionName)
    {
        var endPos = (long)reader.Position + byteLength;
        if (endPos > reader.Length)
        {
            throw new InvalidDataException(
                $"Declared {sectionName} length {byteLength} exceeds remaining buffer " +
                $"({reader.Length - reader.Position} bytes available).");
        }

        return (int)endPos;
    }

    // --- Array/Dict instance creation (mirrors LibDBusMessageMarshaler) ---

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

    private static List<T> CreateList<T>(IReadOnlyList<object> items)
    {
        var list = new List<T>(items.Count);
        foreach (var item in items)
        {
            list.Add((T)item);
        }
        return list;
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
}
