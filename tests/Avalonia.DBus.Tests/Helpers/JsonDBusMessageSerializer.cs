using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// JSON-based serializer for <see cref="DBusMessage"/>.
/// Uses the D-Bus signature to guide type reconstruction on deserialization.
/// </summary>
internal sealed class JsonDBusMessageSerializer : IDBusMessageSerializer
{
    public void Serialize(DBusMessage message, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(stream);

        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("Type", (int)message.Type);
        writer.WriteNumber("Flags", (int)message.Flags);
        writer.WriteNumber("Serial", message.Serial);
        writer.WriteNumber("ReplySerial", message.ReplySerial);

        if (message.Path.HasValue)
            writer.WriteString("Path", message.Path.Value.Value);
        if (message.Interface != null)
            writer.WriteString("Interface", message.Interface);
        if (message.Member != null)
            writer.WriteString("Member", message.Member);
        if (message.ErrorName != null)
            writer.WriteString("ErrorName", message.ErrorName);
        if (message.Destination != null)
            writer.WriteString("Destination", message.Destination);
        if (message.Sender != null)
            writer.WriteString("Sender", message.Sender);

        var sig = message.Signature.Value;
        if (!string.IsNullOrEmpty(sig))
            writer.WriteString("Signature", sig);

        if (message.Body.Count > 0)
        {
            writer.WritePropertyName("Body");
            writer.WriteStartArray();
            foreach (var element in message.Body)
                WriteValue(writer, element);
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    public DBusMessage Deserialize(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        var msg = new DBusMessage
        {
            Type = (DBusMessageType)root.GetProperty("Type").GetInt32(),
            Flags = (DBusMessageFlags)root.GetProperty("Flags").GetInt32(),
            ReplySerial = root.GetProperty("ReplySerial").GetUInt32(),
            Path = root.TryGetProperty("Path", out var pathEl) ? new DBusObjectPath(pathEl.GetString()!) : (DBusObjectPath?)null,
            Interface = root.TryGetProperty("Interface", out var ifaceEl) ? ifaceEl.GetString() : null,
            Member = root.TryGetProperty("Member", out var memberEl) ? memberEl.GetString() : null,
            ErrorName = root.TryGetProperty("ErrorName", out var errEl) ? errEl.GetString() : null,
            Destination = root.TryGetProperty("Destination", out var destEl) ? destEl.GetString() : null,
            Serial = root.GetProperty("Serial").GetUInt32()
        };

        if (root.TryGetProperty("Sender", out var senderEl))
            msg.Sender = senderEl.GetString();

        var signature = root.TryGetProperty("Signature", out var sigEl) ? sigEl.GetString() ?? string.Empty : string.Empty;
        var body = DeserializeBody(root, signature);
        msg.SetBodyWithSignature(body, signature);

        return msg;
    }

    // --- Serialization (write) ---

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case byte v: writer.WriteNumberValue(v); break;
            case bool v: writer.WriteBooleanValue(v); break;
            case short v: writer.WriteNumberValue(v); break;
            case ushort v: writer.WriteNumberValue(v); break;
            case int v: writer.WriteNumberValue(v); break;
            case uint v: writer.WriteNumberValue(v); break;
            case long v: writer.WriteNumberValue(v); break;
            case ulong v: writer.WriteNumberValue(v); break;
            case double v: writer.WriteNumberValue(v); break;
            case string v: writer.WriteStringValue(v); break;
            case DBusObjectPath v: writer.WriteStringValue(v.Value); break;
            case DBusSignature v: writer.WriteStringValue(v.Value); break;
            case DBusUnixFd v: writer.WriteNumberValue(v.Fd); break;
            case DBusVariant v: WriteVariant(writer, v); break;
            case DBusStruct v: WriteStruct(writer, v); break;
            case IDictionary v: WriteDictionary(writer, v); break;
            case IList v: WriteList(writer, v); break;
            default:
                throw new NotSupportedException($"Unsupported D-Bus value type for serialization: {value.GetType().FullName}");
        }
    }

    private static void WriteVariant(Utf8JsonWriter writer, DBusVariant variant)
    {
        writer.WriteStartObject();
        writer.WriteString("Signature", variant.Signature.Value);
        writer.WritePropertyName("Value");
        WriteValue(writer, variant.Value);
        writer.WriteEndObject();
    }

    private static void WriteStruct(Utf8JsonWriter writer, DBusStruct s)
    {
        writer.WriteStartArray();
        foreach (var field in s)
            WriteValue(writer, field);
        writer.WriteEndArray();
    }

    private static void WriteDictionary(Utf8JsonWriter writer, IDictionary dict)
    {
        writer.WriteStartObject();
        foreach (DictionaryEntry entry in dict)
        {
            writer.WritePropertyName(entry.Key.ToString() ?? string.Empty);
            WriteValue(writer, entry.Value!);
        }
        writer.WriteEndObject();
    }

    private static void WriteList(Utf8JsonWriter writer, IList list)
    {
        writer.WriteStartArray();
        foreach (var item in list)
            WriteValue(writer, item!);
        writer.WriteEndArray();
    }

    // --- Deserialization (read) ---

    private static IReadOnlyList<object> DeserializeBody(JsonElement root, string signature)
    {
        if (!root.TryGetProperty("Body", out var bodyEl) || bodyEl.ValueKind != JsonValueKind.Array)
            return [];

        if (string.IsNullOrEmpty(signature))
            return [];

        var result = new List<object>(bodyEl.GetArrayLength());
        var sigIndex = 0;

        foreach (var element in bodyEl.EnumerateArray())
        {
            var elementSignature = DBusSignatureParser.ReadSingleType(signature, ref sigIndex);
            result.Add(DeserializeElement(element, elementSignature));
        }

        return result;
    }

    private static object DeserializeElement(JsonElement element, string signature)
    {
        DBusSignatureToken token = signature[0];
        return token switch
        {
            _ when token == DBusSignatureToken.Byte => element.GetByte(),
            _ when token == DBusSignatureToken.Boolean => element.GetBoolean(),
            _ when token == DBusSignatureToken.Int16 => element.GetInt16(),
            _ when token == DBusSignatureToken.UInt16 => element.GetUInt16(),
            _ when token == DBusSignatureToken.Int32 => element.GetInt32(),
            _ when token == DBusSignatureToken.UInt32 => element.GetUInt32(),
            _ when token == DBusSignatureToken.Int64 => element.GetInt64(),
            _ when token == DBusSignatureToken.UInt64 => element.GetUInt64(),
            _ when token == DBusSignatureToken.Double => element.GetDouble(),
            _ when token == DBusSignatureToken.String => element.GetString()!,
            _ when token == DBusSignatureToken.ObjectPath => new DBusObjectPath(element.GetString()!),
            _ when token == DBusSignatureToken.Signature => new DBusSignature(element.GetString()!),
            _ when token == DBusSignatureToken.UnixFd => new DBusUnixFd(element.GetInt32()),
            _ when token == DBusSignatureToken.Variant => DeserializeVariant(element),
            _ when token == DBusSignatureToken.StructBegin => DeserializeStruct(element, signature),
            _ when token == DBusSignatureToken.Array => DeserializeArrayOrDict(element, signature),
            _ => throw new NotSupportedException($"Unsupported D-Bus signature token for deserialization: {signature[0]}")
        };
    }

    private static object DeserializeArrayOrDict(JsonElement element, string signature)
    {
        var index = 1;
        var innerSignature = DBusSignatureParser.ReadSingleType(signature, ref index);

        return innerSignature[0] == DBusSignatureToken.DictEntryBegin
            ? DeserializeDictionary(element, innerSignature)
            : DeserializeList(element, innerSignature);
    }

    private static DBusVariant DeserializeVariant(JsonElement element)
    {
        var sig = element.GetProperty("Signature").GetString()
            ?? throw new InvalidOperationException("DBusVariant missing Signature.");
        var valueEl = element.GetProperty("Value");
        var innerValue = DeserializeElement(valueEl, sig);
        return new DBusVariant(innerValue);
    }

    private static DBusStruct DeserializeStruct(JsonElement element, string signature)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Expected JSON array for DBusStruct.");

        var memberSignatures = DBusSignatureParser.ParseStructSignatures(signature);
        var fields = new List<object>(element.GetArrayLength());
        var i = 0;

        foreach (var item in element.EnumerateArray().TakeWhile(_ => i < memberSignatures.Count))
        {
            fields.Add(DeserializeElement(item, memberSignatures[i]));
            i++;
        }

        return new DBusStruct(fields);
    }

    // --- Dictionary deserialization ---

    private static object DeserializeDictionary(JsonElement element, string entrySignature)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Expected JSON object for Dictionary.");

        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySignature);

        DBusSignatureToken keyToken = keySig[0];
        return keyToken switch
        {
            _ when keyToken == DBusSignatureToken.String => DeserializeDictWithKey<string>(element, valueSig, s => s),
            _ when keyToken == DBusSignatureToken.Byte => DeserializeDictWithKey(element, valueSig, byte.Parse),
            _ when keyToken == DBusSignatureToken.Boolean => DeserializeDictWithKey(element, valueSig, bool.Parse),
            _ when keyToken == DBusSignatureToken.Int16 => DeserializeDictWithKey(element, valueSig, short.Parse),
            _ when keyToken == DBusSignatureToken.UInt16 => DeserializeDictWithKey(element, valueSig, ushort.Parse),
            _ when keyToken == DBusSignatureToken.Int32 => DeserializeDictWithKey(element, valueSig, int.Parse),
            _ when keyToken == DBusSignatureToken.UInt32 => DeserializeDictWithKey(element, valueSig, uint.Parse),
            _ when keyToken == DBusSignatureToken.Int64 => DeserializeDictWithKey(element, valueSig, long.Parse),
            _ when keyToken == DBusSignatureToken.UInt64 => DeserializeDictWithKey(element, valueSig, ulong.Parse),
            _ when keyToken == DBusSignatureToken.Double => DeserializeDictWithKey(element, valueSig, double.Parse),
            _ when keyToken == DBusSignatureToken.ObjectPath => DeserializeDictWithKey(element, valueSig, s => new DBusObjectPath(s)),
            _ when keyToken == DBusSignatureToken.Signature => DeserializeDictWithKey(element, valueSig, s => new DBusSignature(s)),
            _ => throw new NotSupportedException($"Unsupported D-Bus dictionary key signature: {keySig}")
        };
    }

    private static object DeserializeDictWithKey<TKey>(JsonElement element, string valueSig, Func<string, TKey> parseKey) where TKey : notnull
    {
        DBusSignatureToken valueToken = valueSig[0];
        return valueToken switch
        {
            _ when valueToken == DBusSignatureToken.Byte => FillDict<TKey, byte>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Boolean => FillDict<TKey, bool>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Int16 => FillDict<TKey, short>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.UInt16 => FillDict<TKey, ushort>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Int32 => FillDict<TKey, int>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.UInt32 => FillDict<TKey, uint>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Int64 => FillDict<TKey, long>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.UInt64 => FillDict<TKey, ulong>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Double => FillDict<TKey, double>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.String => FillDict<TKey, string>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.ObjectPath => FillDict<TKey, DBusObjectPath>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Signature => FillDict<TKey, DBusSignature>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.UnixFd => FillDict<TKey, DBusUnixFd>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Variant => FillDict<TKey, DBusVariant>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.StructBegin => FillDict<TKey, DBusStruct>(element, valueSig, parseKey),
            _ when valueToken == DBusSignatureToken.Array => FillDictBoxedValues(element, valueSig, parseKey),
            _ => throw new NotSupportedException($"Unsupported D-Bus dictionary value signature: {valueSig}")
        };
    }

    private static Dictionary<TKey, TValue> FillDict<TKey, TValue>(
        JsonElement element, string valueSig, Func<string, TKey> parseKey) where TKey : notnull
    {
        var dict = new Dictionary<TKey, TValue>();
        foreach (var prop in element.EnumerateObject())
            dict[parseKey(prop.Name)] = (TValue)DeserializeElement(prop.Value, valueSig);
        return dict;
    }

    private static object FillDictBoxedValues<TKey>(
        JsonElement element, string valueSig, Func<string, TKey> parseKey) where TKey : notnull
    {
        var index = 1;
        var innerSig = DBusSignatureParser.ReadSingleType(valueSig, ref index);

        return innerSig[0] == DBusSignatureToken.DictEntryBegin ? FillDictWithNestedDictValue(element, valueSig, parseKey, innerSig) :
            // a{s as} — value is a list
            FillDictWithListValue(element, valueSig, parseKey, innerSig);
    }

    private static object FillDictWithListValue<TKey>(
        JsonElement element, string valueSig, Func<string, TKey> parseKey, string elementSig) where TKey : notnull
    {
        DBusSignatureToken elemToken = elementSig[0];
        return elemToken switch
        {
            _ when elemToken == DBusSignatureToken.Byte => FillDict<TKey, List<byte>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Boolean => FillDict<TKey, List<bool>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Int16 => FillDict<TKey, List<short>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.UInt16 => FillDict<TKey, List<ushort>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Int32 => FillDict<TKey, List<int>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.UInt32 => FillDict<TKey, List<uint>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Int64 => FillDict<TKey, List<long>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.UInt64 => FillDict<TKey, List<ulong>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Double => FillDict<TKey, List<double>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.String => FillDict<TKey, List<string>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.ObjectPath => FillDict<TKey, List<DBusObjectPath>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Signature => FillDict<TKey, List<DBusSignature>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.UnixFd => FillDict<TKey, List<DBusUnixFd>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.Variant => FillDict<TKey, List<DBusVariant>>(element, valueSig, parseKey),
            _ when elemToken == DBusSignatureToken.StructBegin => FillDict<TKey, List<DBusStruct>>(element, valueSig, parseKey),
            _ => throw new NotSupportedException($"Unsupported nested list element in dict value: {elementSig}")
        };
    }

    private static object FillDictWithNestedDictValue<TKey>(
        JsonElement element, string valueSig, Func<string, TKey> parseKey, string entrySig) where TKey : notnull
    {
        var (innerKeySig, innerValueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySig);

        DBusSignatureToken innerKeyToken = innerKeySig[0];
        DBusSignatureToken innerValueToken = innerValueSig[0];
        return innerKeyToken switch
        {
            _ when innerKeyToken == DBusSignatureToken.String && innerValueToken == DBusSignatureToken.Variant
                => FillDict<TKey, Dictionary<string, DBusVariant>>(element, valueSig, parseKey),
            _ when innerKeyToken == DBusSignatureToken.String && innerValueToken == DBusSignatureToken.String
                => FillDict<TKey, Dictionary<string, string>>(element, valueSig, parseKey),
            _ when innerKeyToken == DBusSignatureToken.String && innerValueToken == DBusSignatureToken.Int32
                => FillDict<TKey, Dictionary<string, int>>(element, valueSig, parseKey),
            _ => FillDictFallback(element, valueSig, parseKey)
        };
    }

    private static Dictionary<TKey, object> FillDictFallback<TKey>(
        JsonElement element, string valueSig, Func<string, TKey> parseKey) where TKey : notnull
    {
        var dict = new Dictionary<TKey, object>();
        foreach (var prop in element.EnumerateObject())
            dict[parseKey(prop.Name)] = DeserializeElement(prop.Value, valueSig);
        return dict;
    }

    // --- List deserialization ---

    private static object DeserializeList(JsonElement element, string elementSignature)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Expected JSON array for List.");

        DBusSignatureToken elemToken = elementSignature[0];
        return elemToken switch
        {
            _ when elemToken == DBusSignatureToken.Byte => FillList<byte>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Boolean => FillList<bool>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Int16 => FillList<short>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.UInt16 => FillList<ushort>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Int32 => FillList<int>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.UInt32 => FillList<uint>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Int64 => FillList<long>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.UInt64 => FillList<ulong>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Double => FillList<double>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.String => FillList<string>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.ObjectPath => FillList<DBusObjectPath>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Signature => FillList<DBusSignature>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.UnixFd => FillList<DBusUnixFd>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Variant => FillList<DBusVariant>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.StructBegin => FillList<DBusStruct>(element, elementSignature),
            _ when elemToken == DBusSignatureToken.Array => DeserializeNestedList(element, elementSignature),
            _ when elemToken == DBusSignatureToken.DictEntryBegin => DeserializeDictionary(element, elementSignature),
            _ => throw new NotSupportedException($"Unsupported list element signature: {elementSignature}")
        };
    }

    private static List<T> FillList<T>(JsonElement element, string elementSignature)
    {
        var list = new List<T>(element.GetArrayLength());
        list.AddRange(element.EnumerateArray()
            .Select(item => (T)DeserializeElement(item, elementSignature)));
        return list;
    }

    private static object DeserializeNestedList(JsonElement element, string arraySignature)
    {
        var index = 1;
        var innerSig = DBusSignatureParser.ReadSingleType(arraySignature, ref index);

        if (innerSig[0] == DBusSignatureToken.DictEntryBegin)
            return DeserializeListOfDicts(element, arraySignature, innerSig);

        // List of lists
        DBusSignatureToken innerToken = innerSig[0];
        return innerToken switch
        {
            _ when innerToken == DBusSignatureToken.Byte => FillNestedList<byte>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Boolean => FillNestedList<bool>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Int16 => FillNestedList<short>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.UInt16 => FillNestedList<ushort>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Int32 => FillNestedList<int>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.UInt32 => FillNestedList<uint>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Int64 => FillNestedList<long>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.UInt64 => FillNestedList<ulong>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Double => FillNestedList<double>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.String => FillNestedList<string>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.ObjectPath => FillNestedList<DBusObjectPath>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Signature => FillNestedList<DBusSignature>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.UnixFd => FillNestedList<DBusUnixFd>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.Variant => FillNestedList<DBusVariant>(element, arraySignature),
            _ when innerToken == DBusSignatureToken.StructBegin => FillNestedList<DBusStruct>(element, arraySignature),
            _ => throw new NotSupportedException($"Unsupported nested list element signature: {innerSig}")
        };
    }

    private static List<List<T>> FillNestedList<T>(JsonElement element, string arraySignature)
    {
        var list = new List<List<T>>(element.GetArrayLength());
        list.AddRange(element.EnumerateArray().Select(item => (List<T>)DeserializeList(item, arraySignature[1..])));
        return list;
    }

    private static object DeserializeListOfDicts(JsonElement element, string fullSig, string entrySig)
    {
        var (keySig, valueSig) = DBusSignatureParser.ParseDictEntrySignatures(entrySig);

        DBusSignatureToken keyToken = keySig[0];
        DBusSignatureToken valToken = valueSig[0];
        return keyToken switch
        {
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.Variant
                => FillListOfTypedDicts<string, DBusVariant>(element, fullSig),
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.String
                => FillListOfTypedDicts<string, string>(element, fullSig),
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.Int32
                => FillListOfTypedDicts<string, int>(element, fullSig),
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.UInt32
                => FillListOfTypedDicts<string, uint>(element, fullSig),
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.Boolean
                => FillListOfTypedDicts<string, bool>(element, fullSig),
            _ when keyToken == DBusSignatureToken.String && valToken == DBusSignatureToken.ObjectPath
                => FillListOfTypedDicts<string, DBusObjectPath>(element, fullSig),
            _ => FillListOfDictsFallback(element, entrySig)
        };
    }

    private static List<object> FillListOfDictsFallback(JsonElement element, string entrySig)
    {
        var list = new List<object>(element.GetArrayLength());
        list.AddRange(element.EnumerateArray().Select(item => DeserializeDictionary(item, entrySig)));
        return list;
    }

    private static List<Dictionary<TKey, TValue>> FillListOfTypedDicts<TKey, TValue>(
        JsonElement element, string fullSig) where TKey : notnull
    {
        var list = new List<Dictionary<TKey, TValue>>(element.GetArrayLength());
        list.AddRange(element.EnumerateArray().Select(item => (Dictionary<TKey, TValue>)DeserializeDictionary(item, fullSig[1..])));
        return list;
    }
}
