using System;
using System.Collections;
using System.Collections.Generic;
using global::Tmds.DBus.Protocol;

namespace Avalonia.DBus.Tmds;

/// <summary>
/// Bidirectional converter between Avalonia.DBus CLR-boxed body values
/// and Tmds.DBus.Protocol's binary Reader/MessageWriter.
/// All conversions are signature-driven: we walk the D-Bus type signature
/// and read/write the corresponding values.
/// </summary>
internal static class TmdsTypeConverter
{
    /// <summary>
    /// Reads the body of a Tmds <see cref="Message"/> into an Avalonia.DBus body list.
    /// </summary>
    internal static IReadOnlyList<object> ReadBody(Message message)
    {
        var signature = message.SignatureAsString;
        if (string.IsNullOrEmpty(signature))
            return [];

        var reader = message.GetBodyReader();
        var result = new List<object>();
        var index = 0;

        while (index < signature.Length)
        {
            var typeSig = DBusSignatureParser.ReadSingleType(signature, ref index);
            result.Add(ReadValue(ref reader, typeSig, 0));
        }

        return result;
    }

    /// <summary>
    /// Writes Avalonia.DBus body items to a Tmds <see cref="MessageWriter"/>.
    /// </summary>
    internal static void WriteBody(ref MessageWriter writer, IReadOnlyList<object> body, string signature)
    {
        if (body.Count == 0 || string.IsNullOrEmpty(signature))
            return;

        var index = 0;
        var bodyIndex = 0;

        while (index < signature.Length && bodyIndex < body.Count)
        {
            var typeSig = DBusSignatureParser.ReadSingleType(signature, ref index);
            WriteValue(ref writer, body[bodyIndex], typeSig);
            bodyIndex++;
        }
    }

    /// <summary>
    /// Reads a single value from the reader based on a D-Bus type signature.
    /// </summary>
    private static object ReadValue(ref Reader reader, string signature, int depth)
    {
        if (depth > 64)
            throw new InvalidOperationException("D-Bus type nesting depth exceeded.");

        var token = signature[0];

        return token switch
        {
            'y' => reader.ReadByte(),
            'b' => reader.ReadBool(),
            'n' => reader.ReadInt16(),
            'q' => reader.ReadUInt16(),
            'i' => reader.ReadInt32(),
            'u' => reader.ReadUInt32(),
            'x' => reader.ReadInt64(),
            't' => reader.ReadUInt64(),
            'd' => reader.ReadDouble(),
            's' => reader.ReadString(),
            'o' => (object)new DBusObjectPath(reader.ReadObjectPathAsString()),
            'g' => (object)new DBusSignature(reader.ReadSignatureAsString()),
            'h' => (object)new DBusUnixFd((int)reader.ReadUInt32()),
            'v' => ReadVariant(ref reader, depth),
            '(' => ReadStruct(ref reader, signature, depth),
            'a' => ReadArray(ref reader, signature, depth),
            _ => throw new NotSupportedException($"Unsupported D-Bus type token: {token}")
        };
    }

    private static object ReadVariant(ref Reader reader, int depth)
    {
        var varSig = reader.ReadSignatureAsString();
        if (string.IsNullOrEmpty(varSig))
            throw new InvalidOperationException("Variant has empty signature.");

        var value = ReadValue(ref reader, varSig, depth + 1);
        return new DBusVariant(value, varSig);
    }

    private static object ReadStruct(ref Reader reader, string signature, int depth)
    {
        var fieldSignatures = DBusSignatureParser.ParseStructSignatures(signature);
        reader.AlignStruct();
        var fields = new object[fieldSignatures.Count];

        for (var i = 0; i < fieldSignatures.Count; i++)
            fields[i] = ReadValue(ref reader, fieldSignatures[i], depth + 1);

        return new DBusStruct(fields);
    }

    private static object ReadArray(ref Reader reader, string signature, int depth)
    {
        // signature starts with 'a', element type follows
        var elementIndex = 1;
        var elementSig = DBusSignatureParser.ReadSingleType(signature, ref elementIndex);

        if (elementSig[0] == '{')
            return ReadDictionary(ref reader, elementSig, depth);

        return ReadTypedArray(ref reader, elementSig, depth);
    }

    private static object ReadDictionary(ref Reader reader, string entrySig, int depth)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySig);

        // Use concrete dictionary types for common key types
        return keySig switch
        {
            "s" => ReadDictWith<string>(ref reader, keySig, valueSig, depth),
            "i" => ReadDictWith<int>(ref reader, keySig, valueSig, depth),
            "u" => ReadDictWith<uint>(ref reader, keySig, valueSig, depth),
            "y" => ReadDictWith<byte>(ref reader, keySig, valueSig, depth),
            "b" => ReadDictWith<bool>(ref reader, keySig, valueSig, depth),
            "n" => ReadDictWith<short>(ref reader, keySig, valueSig, depth),
            "q" => ReadDictWith<ushort>(ref reader, keySig, valueSig, depth),
            "x" => ReadDictWith<long>(ref reader, keySig, valueSig, depth),
            "t" => ReadDictWith<ulong>(ref reader, keySig, valueSig, depth),
            "d" => ReadDictWith<double>(ref reader, keySig, valueSig, depth),
            "o" => ReadDictWith<DBusObjectPath>(ref reader, keySig, valueSig, depth),
            _ => ReadDictGeneric(ref reader, keySig, valueSig, depth)
        };
    }

    private static object ReadDictWith<TKey>(ref Reader reader, string keySig, string valueSig, int depth)
        where TKey : notnull
    {
        // Try typed values for common cases
        return valueSig switch
        {
            "s" => ReadDictPair<TKey, string>(ref reader, keySig, valueSig, depth),
            "v" => ReadDictPairVariantValue<TKey>(ref reader, keySig, depth),
            "i" => ReadDictPair<TKey, int>(ref reader, keySig, valueSig, depth),
            "u" => ReadDictPair<TKey, uint>(ref reader, keySig, valueSig, depth),
            "b" => ReadDictPair<TKey, bool>(ref reader, keySig, valueSig, depth),
            "x" => ReadDictPair<TKey, long>(ref reader, keySig, valueSig, depth),
            "t" => ReadDictPair<TKey, ulong>(ref reader, keySig, valueSig, depth),
            "d" => ReadDictPair<TKey, double>(ref reader, keySig, valueSig, depth),
            "y" => ReadDictPair<TKey, byte>(ref reader, keySig, valueSig, depth),
            "n" => ReadDictPair<TKey, short>(ref reader, keySig, valueSig, depth),
            "q" => ReadDictPair<TKey, ushort>(ref reader, keySig, valueSig, depth),
            _ => ReadDictPairBoxed<TKey>(ref reader, keySig, valueSig, depth)
        };
    }

    private static Dictionary<TKey, TValue> ReadDictPair<TKey, TValue>(
        ref Reader reader, string keySig, string valueSig, int depth)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(arrayEnd))
        {
            reader.AlignStruct();
            var key = (TKey)ReadValue(ref reader, keySig, depth + 1);
            var value = (TValue)ReadValue(ref reader, valueSig, depth + 1);
            dict[key] = value;
        }
        return dict;
    }

    private static Dictionary<TKey, object> ReadDictPairVariantValue<TKey>(
        ref Reader reader, string keySig, int depth)
        where TKey : notnull
    {
        // For variant values, unwrap the variant to match Avalonia.DBus convention (Dict<K, object>)
        var dict = new Dictionary<TKey, object>();
        var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(arrayEnd))
        {
            reader.AlignStruct();
            var key = (TKey)ReadValue(ref reader, keySig, depth + 1);
            var variant = (DBusVariant)ReadValue(ref reader, "v", depth + 1);
            dict[key] = variant;
            }
        return dict;
    }

    private static Dictionary<TKey, object> ReadDictPairBoxed<TKey>(
        ref Reader reader, string keySig, string valueSig, int depth)
        where TKey : notnull
    {
        var dict = new Dictionary<TKey, object>();
        var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(arrayEnd))
        {
            reader.AlignStruct();
            var key = (TKey)ReadValue(ref reader, keySig, depth + 1);
            var value = ReadValue(ref reader, valueSig, depth + 1);
            dict[key] = value;
        }
        return dict;
    }

    private static object ReadDictGeneric(ref Reader reader, string keySig, string valueSig, int depth)
    {
        var dict = new Dictionary<object, object>();
        var arrayEnd = reader.ReadArrayStart(DBusType.Struct);
        while (reader.HasNext(arrayEnd))
        {
            reader.AlignStruct();
            var key = ReadValue(ref reader, keySig, depth + 1);
            var value = ReadValue(ref reader, valueSig, depth + 1);
            dict[key] = value;
        }
        return dict;
    }

    private static object ReadTypedArray(ref Reader reader, string elementSig, int depth)
    {
        // Produce typed List<T> for primitive element types
        return elementSig[0] switch
        {
            'y' => ReadList<byte>(ref reader, elementSig, depth),
            'b' => ReadList<bool>(ref reader, elementSig, depth),
            'n' => ReadList<short>(ref reader, elementSig, depth),
            'q' => ReadList<ushort>(ref reader, elementSig, depth),
            'i' => ReadList<int>(ref reader, elementSig, depth),
            'u' => ReadList<uint>(ref reader, elementSig, depth),
            'x' => ReadList<long>(ref reader, elementSig, depth),
            't' => ReadList<ulong>(ref reader, elementSig, depth),
            'd' => ReadList<double>(ref reader, elementSig, depth),
            's' => ReadList<string>(ref reader, elementSig, depth),
            'o' => ReadList<DBusObjectPath>(ref reader, elementSig, depth),
            'g' => ReadList<DBusSignature>(ref reader, elementSig, depth),
            'v' => ReadList<DBusVariant>(ref reader, elementSig, depth),
            '(' => ReadList<DBusStruct>(ref reader, elementSig, depth),
            'a' => ReadNestedArrayList(ref reader, elementSig, depth),
            _ => ReadListBoxed(ref reader, elementSig, depth)
        };
    }

    private static object ReadNestedArrayList(ref Reader reader, string elementSig, int depth)
    {
        // elementSig starts with 'a' — determine the inner element type
        var innerIdx = 1;
        var innerElementSig = DBusSignatureParser.ReadSingleType(elementSig, ref innerIdx);

        if (innerElementSig[0] == '{')
        {
            // Array of dictionaries — fall back to boxed list
            return ReadListBoxed(ref reader, elementSig, depth);
        }

        // Produce typed List<List<T>> for common inner element types
        return innerElementSig[0] switch
        {
            'y' => ReadList<List<byte>>(ref reader, elementSig, depth),
            'b' => ReadList<List<bool>>(ref reader, elementSig, depth),
            'n' => ReadList<List<short>>(ref reader, elementSig, depth),
            'q' => ReadList<List<ushort>>(ref reader, elementSig, depth),
            'i' => ReadList<List<int>>(ref reader, elementSig, depth),
            'u' => ReadList<List<uint>>(ref reader, elementSig, depth),
            'x' => ReadList<List<long>>(ref reader, elementSig, depth),
            't' => ReadList<List<ulong>>(ref reader, elementSig, depth),
            'd' => ReadList<List<double>>(ref reader, elementSig, depth),
            's' => ReadList<List<string>>(ref reader, elementSig, depth),
            'o' => ReadList<List<DBusObjectPath>>(ref reader, elementSig, depth),
            'g' => ReadList<List<DBusSignature>>(ref reader, elementSig, depth),
            'v' => ReadList<List<DBusVariant>>(ref reader, elementSig, depth),
            '(' => ReadList<List<DBusStruct>>(ref reader, elementSig, depth),
            _ => ReadListBoxed(ref reader, elementSig, depth)
        };
    }

    private static List<T> ReadList<T>(ref Reader reader, string elementSig, int depth)
    {
        var list = new List<T>();
        var dbusType = GetDBusTypeForSignature(elementSig[0]);
        var arrayEnd = reader.ReadArrayStart(dbusType);
        while (reader.HasNext(arrayEnd))
        {
            list.Add((T)ReadValue(ref reader, elementSig, depth + 1));
        }
        return list;
    }

    private static List<object> ReadListBoxed(ref Reader reader, string elementSig, int depth)
    {
        var list = new List<object>();
        var dbusType = GetDBusTypeForSignature(elementSig[0]);
        var arrayEnd = reader.ReadArrayStart(dbusType);
        while (reader.HasNext(arrayEnd))
        {
            list.Add(ReadValue(ref reader, elementSig, depth + 1));
        }
        return list;
    }

    // --- Write side ---

    /// <summary>
    /// Writes a single value to the writer based on a D-Bus type signature.
    /// </summary>
    private static void WriteValue(ref MessageWriter writer, object value, string signature)
    {
        var token = signature[0];

        switch (token)
        {
            case 'y':
                writer.WriteByte(Convert.ToByte(value));
                break;
            case 'b':
                writer.WriteBool(Convert.ToBoolean(value));
                break;
            case 'n':
                writer.WriteInt16(Convert.ToInt16(value));
                break;
            case 'q':
                writer.WriteUInt16(Convert.ToUInt16(value));
                break;
            case 'i':
                writer.WriteInt32(Convert.ToInt32(value));
                break;
            case 'u':
                writer.WriteUInt32(Convert.ToUInt32(value));
                break;
            case 'x':
                writer.WriteInt64(Convert.ToInt64(value));
                break;
            case 't':
                writer.WriteUInt64(Convert.ToUInt64(value));
                break;
            case 'd':
                writer.WriteDouble(Convert.ToDouble(value));
                break;
            case 's':
                writer.WriteString((string)value);
                break;
            case 'o':
                writer.WriteObjectPath(((DBusObjectPath)value).Value);
                break;
            case 'g':
                writer.WriteSignature(((DBusSignature)value).Value);
                break;
            case 'h':
                writer.WriteUInt32((uint)((DBusUnixFd)value).Fd);
                break;
            case 'v':
                WriteVariant(ref writer, value);
                break;
            case '(':
                WriteStruct(ref writer, value, signature);
                break;
            case 'a':
                WriteArray(ref writer, value, signature);
                break;
            default:
                throw new NotSupportedException($"Unsupported D-Bus type token for write: {token}");
        }
    }

    private static void WriteVariant(ref MessageWriter writer, object value)
    {
        DBusVariant variant;
        if (value is DBusVariant v)
        {
            variant = v;
        }
        else
        {
            variant = new DBusVariant(value);
        }

        var innerSig = variant.Signature.Value;
        writer.WriteSignature(innerSig);
        WriteValue(ref writer, variant.Value, innerSig);
    }

    private static void WriteStruct(ref MessageWriter writer, object value, string signature)
    {
        var fieldSignatures = DBusSignatureParser.ParseStructSignatures(signature);

        IReadOnlyList<object> fields;
        if (value is DBusStruct dbusStruct)
        {
            fields = dbusStruct;
        }
        else if (value is IDBusStructConvertible convertible)
        {
            fields = convertible.ToDbusStruct();
        }
        else
        {
            throw new NotSupportedException($"Cannot write struct from type: {value.GetType().FullName}");
        }

        writer.WriteStructureStart();
        for (var i = 0; i < fieldSignatures.Count && i < fields.Count; i++)
        {
            WriteValue(ref writer, fields[i], fieldSignatures[i]);
        }
    }

    private static void WriteArray(ref MessageWriter writer, object value, string signature)
    {
        // signature starts with 'a', element type follows
        var elementIndex = 1;
        var elementSig = DBusSignatureParser.ReadSingleType(signature, ref elementIndex);

        if (elementSig[0] == '{')
        {
            WriteDictionary(ref writer, value, elementSig);
        }
        else
        {
            WriteTypedArray(ref writer, value, elementSig);
        }
    }

    private static void WriteDictionary(ref MessageWriter writer, object value, string entrySig)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySig);

        var arrayStart = writer.WriteArrayStart(DBusType.Struct);

        if (value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                writer.WriteStructureStart();
                WriteValue(ref writer, entry.Key!, keySig);
                WriteValue(ref writer, entry.Value!, valueSig);
            }
        }

        writer.WriteArrayEnd(arrayStart);
    }

    private static void WriteTypedArray(ref MessageWriter writer, object value, string elementSig)
    {
        var dbusType = GetDBusTypeForSignature(elementSig[0]);
        var arrayStart = writer.WriteArrayStart(dbusType);

        if (value is IList list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                WriteValue(ref writer, list[i]!, elementSig);
            }
        }
        else if (value is Array array)
        {
            for (var i = 0; i < array.Length; i++)
            {
                WriteValue(ref writer, array.GetValue(i)!, elementSig);
            }
        }

        writer.WriteArrayEnd(arrayStart);
    }

    private static DBusType GetDBusTypeForSignature(char token)
    {
        return token switch
        {
            'y' => DBusType.Byte,
            'b' => DBusType.Bool,
            'n' => DBusType.Int16,
            'q' => DBusType.UInt16,
            'i' => DBusType.Int32,
            'u' => DBusType.UInt32,
            'x' => DBusType.Int64,
            't' => DBusType.UInt64,
            'd' => DBusType.Double,
            's' => DBusType.String,
            'o' => DBusType.ObjectPath,
            'g' => DBusType.Signature,
            'h' => DBusType.UnixFd,
            'v' => DBusType.Variant,
            '(' => DBusType.Struct,
            'a' => DBusType.Array,
            '{' => DBusType.DictEntry,
            _ => throw new NotSupportedException($"Unknown D-Bus type token: {token}")
        };
    }

    /// <summary>
    /// Converts a Tmds <see cref="Message"/> to an Avalonia.DBus <see cref="DBusMessage"/>.
    /// </summary>
    internal static DBusMessage ToDBusMessage(Message message)
    {
        var body = ReadBody(message);
        var sig = message.SignatureAsString;

        var result = new DBusMessage
        {
            Type = ConvertMessageType(message.MessageType),
            Flags = ConvertFlags(message.MessageFlags),
            Path = message.PathIsSet ? new DBusObjectPath(message.PathAsString ?? string.Empty) : null,
            Interface = message.InterfaceAsString,
            Member = message.MemberAsString,
            ErrorName = message.ErrorNameAsString,
            Destination = message.DestinationAsString,
            ReplySerial = message.ReplySerial ?? 0,
        };
        result.Sender = message.SenderAsString;
        result.Serial = message.Serial;
        result.SetBodyWithSignature(body, sig);
        return result;
    }

    /// <summary>
    /// Converts an Avalonia.DBus <see cref="DBusMessage"/> into a Tmds <see cref="MessageBuffer"/>
    /// ready for sending.
    /// </summary>
    internal static MessageBuffer ToMessageBuffer(
        DBusMessage message,
        global::Tmds.DBus.Protocol.DBusConnection connection)
    {
        var sig = message.Signature.Value;
        var hasSig = !string.IsNullOrEmpty(sig);

        var writer = connection.GetMessageWriter();
        try
        {
            switch (message.Type)
            {
                case DBusMessageType.MethodCall:
                    writer.WriteMethodCallHeader(
                        destination: message.Destination,
                        path: message.Path?.Value,
                        @interface: message.Interface,
                        member: message.Member,
                        signature: hasSig ? sig : null,
                        flags: ConvertFlagsToTmds(message.Flags));
                    break;

                case DBusMessageType.Signal:
                    writer.WriteSignalHeader(
                        destination: message.Destination,
                        path: message.Path?.Value,
                        @interface: message.Interface,
                        member: message.Member,
                        signature: hasSig ? sig : null);
                    break;

                case DBusMessageType.MethodReturn:
                    writer.WriteMethodReturnHeader(
                        replySerial: message.ReplySerial,
                        destination: message.Destination != null
                            ? System.Text.Encoding.UTF8.GetBytes(message.Destination)
                            : default,
                        signature: hasSig ? sig : null);
                    break;

                case DBusMessageType.Error:
                    writer.WriteError(
                        replySerial: message.ReplySerial,
                        destination: message.Destination != null
                            ? System.Text.Encoding.UTF8.GetBytes(message.Destination)
                            : default,
                        errorName: message.ErrorName,
                        errorMsg: message.Body.Count > 0 && message.Body[0] is string errMsg
                            ? errMsg
                            : null);
                    // WriteError already writes the error message string as body, so skip body writing
                    return writer.CreateMessage();

                default:
                    throw new NotSupportedException($"Unsupported message type: {message.Type}");
            }

            if (message.Body.Count > 0 && hasSig)
                WriteBody(ref writer, message.Body, sig);

            return writer.CreateMessage();
        }
        catch
        {
            writer.Dispose();
            throw;
        }
    }

    private static DBusMessageType ConvertMessageType(MessageType tmdsType)
    {
        return tmdsType switch
        {
            MessageType.MethodCall => DBusMessageType.MethodCall,
            MessageType.MethodReturn => DBusMessageType.MethodReturn,
            MessageType.Error => DBusMessageType.Error,
            MessageType.Signal => DBusMessageType.Signal,
            _ => DBusMessageType.Invalid
        };
    }

    private static DBusMessageFlags ConvertFlags(MessageFlags tmdsFlags)
    {
        var flags = DBusMessageFlags.None;
        if ((tmdsFlags & MessageFlags.NoReplyExpected) != 0)
            flags |= DBusMessageFlags.NoReplyExpected;
        if ((tmdsFlags & MessageFlags.NoAutoStart) != 0)
            flags |= DBusMessageFlags.NoAutoStart;
        return flags;
    }

    private static MessageFlags ConvertFlagsToTmds(DBusMessageFlags flags)
    {
        var tmdsFlags = MessageFlags.None;
        if ((flags & DBusMessageFlags.NoReplyExpected) != 0)
            tmdsFlags |= MessageFlags.NoReplyExpected;
        if ((flags & DBusMessageFlags.NoAutoStart) != 0)
            tmdsFlags |= MessageFlags.NoAutoStart;
        return tmdsFlags;
    }
}
