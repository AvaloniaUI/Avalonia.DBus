using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia.DBus.Managed;
using Xunit;

namespace Avalonia.DBus.Tests.Managed;

public class ManagedDBusMessageSerializerTests
{
    private readonly ManagedDBusMessageSerializer _serializer = new();

    // --- MethodCall with no body ---

    [Fact]
    public void RoundTrip_MethodCall_NoBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 1,
            Path = new DBusObjectPath("/org/freedesktop/DBus"),
            Interface = "org.freedesktop.DBus",
            Member = "Hello",
            Destination = "org.freedesktop.DBus"
        };

        var serialized = _serializer.Serialize(msg);
        Assert.NotNull(serialized.Message);
        Assert.True(serialized.Message.Length > 0);

        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodCall, result.Type);
        Assert.Equal(1u, result.Serial);
        Assert.Equal("/org/freedesktop/DBus", result.Path?.Value);
        Assert.Equal("org.freedesktop.DBus", result.Interface);
        Assert.Equal("Hello", result.Member);
        Assert.Equal("org.freedesktop.DBus", result.Destination);
        Assert.Empty(result.Body);
    }

    // --- MethodCall with string body ---

    [Fact]
    public void RoundTrip_MethodCall_StringBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 2,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Echo",
            Destination = "com.example.Service",
            Body = new object[] { "hello world" }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodCall, result.Type);
        Assert.Equal(2u, result.Serial);
        Assert.Single(result.Body);
        Assert.Equal("hello world", result.Body[0]);
        Assert.Equal("s", result.Signature.Value);
    }

    // --- MethodCall with multiple int args ---

    [Fact]
    public void RoundTrip_MethodCall_MultipleIntArgs()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 3,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Add",
            Destination = "com.example.Service",
            Body = new object[] { 42, 58 }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(2, result.Body.Count);
        Assert.Equal(42, result.Body[0]);
        Assert.Equal(58, result.Body[1]);
        Assert.Equal("ii", result.Signature.Value);
    }

    // --- Signal ---

    [Fact]
    public void RoundTrip_Signal()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.Signal,
            Serial = 10,
            Path = new DBusObjectPath("/org/example"),
            Interface = "org.example.Signals",
            Member = "SomethingHappened",
            Body = new object[] { "event data", 42u }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.Signal, result.Type);
        Assert.Equal(10u, result.Serial);
        Assert.Equal("/org/example", result.Path?.Value);
        Assert.Equal("org.example.Signals", result.Interface);
        Assert.Equal("SomethingHappened", result.Member);
        Assert.Equal(2, result.Body.Count);
        Assert.Equal("event data", result.Body[0]);
        Assert.Equal(42u, result.Body[1]);
    }

    // --- MethodReturn with ReplySerial ---

    [Fact]
    public void RoundTrip_MethodReturn_WithReplySerial()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 5,
            ReplySerial = 3,
            Destination = ":1.42",
            Body = new object[] { "result" }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodReturn, result.Type);
        Assert.Equal(5u, result.Serial);
        Assert.Equal(3u, result.ReplySerial);
        Assert.Equal(":1.42", result.Destination);
        Assert.Single(result.Body);
        Assert.Equal("result", result.Body[0]);
    }

    // --- Error with ErrorName ---

    [Fact]
    public void RoundTrip_Error_WithErrorName()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.Error,
            Serial = 6,
            ReplySerial = 3,
            ErrorName = "org.freedesktop.DBus.Error.UnknownMethod",
            Destination = ":1.42",
            Body = new object[] { "Method not found" }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.Error, result.Type);
        Assert.Equal(6u, result.Serial);
        Assert.Equal(3u, result.ReplySerial);
        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod", result.ErrorName);
        Assert.Equal(":1.42", result.Destination);
        Assert.Single(result.Body);
        Assert.Equal("Method not found", result.Body[0]);
    }

    // --- Complex body: List<string> ---

    [Fact]
    public void RoundTrip_ListOfStrings()
    {
        var list = new List<string> { "alpha", "beta", "gamma" };
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 7,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "ProcessList",
            Destination = "com.example.Service",
            Body = new object[] { list }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultList = Assert.IsType<List<string>>(result.Body[0]);
        Assert.Equal(3, resultList.Count);
        Assert.Equal("alpha", resultList[0]);
        Assert.Equal("beta", resultList[1]);
        Assert.Equal("gamma", resultList[2]);
    }

    // --- Complex body: Dictionary<string, DBusVariant> ---

    [Fact]
    public void RoundTrip_DictionaryOfStringToVariant()
    {
        var dict = new Dictionary<string, DBusVariant>
        {
            ["name"] = new DBusVariant("Alice"),
            ["age"] = new DBusVariant(30u)
        };
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 8,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "SetProperties",
            Destination = "com.example.Service",
            Body = new object[] { dict }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultDict = Assert.IsType<Dictionary<string, DBusVariant>>(result.Body[0]);
        Assert.Equal(2, resultDict.Count);
        Assert.Equal("Alice", resultDict["name"].Value);
        Assert.Equal(30u, resultDict["age"].Value);
    }

    // --- DBusUnixFd index rewriting ---

    [Fact]
    public void RoundTrip_UnixFd_IndexRewriting()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 9,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "SendFd",
            Destination = "com.example.Service",
            Body = new object[] { new DBusUnixFd(42) }
        };

        var serialized = _serializer.Serialize(msg);

        // The serializer should have extracted the fd into the fds array
        Assert.Single(serialized.Fds);
        Assert.Equal(42, serialized.Fds[0]);

        // Deserialize should restore the fd from the index
        var result = _serializer.Deserialize(serialized);
        Assert.Single(result.Body);
        var fd = Assert.IsType<DBusUnixFd>(result.Body[0]);
        Assert.Equal(42, fd.Fd);
    }

    [Fact]
    public void RoundTrip_MultipleUnixFds()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 10,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "SendFds",
            Destination = "com.example.Service",
            Body = new object[] { new DBusUnixFd(10), new DBusUnixFd(20), new DBusUnixFd(30) }
        };

        var serialized = _serializer.Serialize(msg);

        Assert.Equal(3, serialized.Fds.Length);
        Assert.Equal(10, serialized.Fds[0]);
        Assert.Equal(20, serialized.Fds[1]);
        Assert.Equal(30, serialized.Fds[2]);

        var result = _serializer.Deserialize(serialized);
        Assert.Equal(3, result.Body.Count);
        Assert.Equal(10, ((DBusUnixFd)result.Body[0]).Fd);
        Assert.Equal(20, ((DBusUnixFd)result.Body[1]).Fd);
        Assert.Equal(30, ((DBusUnixFd)result.Body[2]).Fd);
    }

    // --- Flags preserved ---

    [Fact]
    public void RoundTrip_FlagsPreserved()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 11,
            Flags = DBusMessageFlags.NoReplyExpected | DBusMessageFlags.NoAutoStart,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Fire",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageFlags.NoReplyExpected | DBusMessageFlags.NoAutoStart, result.Flags);
    }

    [Fact]
    public void RoundTrip_AllowInteractiveAuthorization_FlagPreserved()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 12,
            Flags = DBusMessageFlags.AllowInteractiveAuthorization,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Authorize",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageFlags.AllowInteractiveAuthorization, result.Flags);
    }

    // --- Nested types: array of structs ---

    [Fact]
    public void RoundTrip_ArrayOfStructs()
    {
        var structs = new List<DBusStruct>
        {
            new("Alice", 30),
            new("Bob", 25)
        };

        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 13,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "ProcessUsers",
            Destination = "com.example.Service",
            Body = new object[] { structs }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultList = Assert.IsType<List<DBusStruct>>(result.Body[0]);
        Assert.Equal(2, resultList.Count);

        Assert.Equal("Alice", resultList[0][0]);
        Assert.Equal(30, resultList[0][1]);
        Assert.Equal("Bob", resultList[1][0]);
        Assert.Equal(25, resultList[1][1]);
    }

    // --- Dict with array values ---

    [Fact]
    public void RoundTrip_DictWithArrayValues()
    {
        var dict = new Dictionary<string, List<int>>
        {
            ["evens"] = new List<int> { 2, 4, 6 },
            ["odds"] = new List<int> { 1, 3, 5 }
        };

        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 14,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "ProcessDict",
            Destination = "com.example.Service",
            Body = new object[] { dict }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultDict = Assert.IsType<Dictionary<string, List<int>>>(result.Body[0]);
        Assert.Equal(2, resultDict.Count);
        Assert.Equal(new List<int> { 2, 4, 6 }, resultDict["evens"]);
        Assert.Equal(new List<int> { 1, 3, 5 }, resultDict["odds"]);
    }

    // --- Wire format structural tests ---

    [Fact]
    public void Serialize_ProducesValidHeader_EndiannessMarker()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 1,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Ping",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);

        // First byte is endianness marker: 'l' for little-endian
        Assert.Equal((byte)'l', serialized.Message[0]);
        // Second byte is message type
        Assert.Equal((byte)DBusMessageType.MethodCall, serialized.Message[1]);
        // Fourth byte is protocol version = 1
        Assert.Equal(1, serialized.Message[3]);
    }

    [Fact]
    public void Serialize_EmptyFds_WhenNoUnixFds()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 1,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Ping",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);

        Assert.Empty(serialized.Fds);
    }

    // --- Various primitive body types ---

    [Theory]
    [InlineData((byte)42)]
    [InlineData((byte)0)]
    [InlineData((byte)255)]
    public void RoundTrip_ByteBody(byte value)
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 20,
            ReplySerial = 1,
            Body = new object[] { value }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(value, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_BooleanBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 21,
            ReplySerial = 1,
            Body = new object[] { true, false }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(2, result.Body.Count);
        Assert.Equal(true, result.Body[0]);
        Assert.Equal(false, result.Body[1]);
    }

    [Fact]
    public void RoundTrip_Int16Body()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 22,
            ReplySerial = 1,
            Body = new object[] { (short)-1234 }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal((short)-1234, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_UInt16Body()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 23,
            ReplySerial = 1,
            Body = new object[] { (ushort)65535 }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal((ushort)65535, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_Int64Body()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 24,
            ReplySerial = 1,
            Body = new object[] { long.MaxValue }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(long.MaxValue, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_UInt64Body()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 25,
            ReplySerial = 1,
            Body = new object[] { ulong.MaxValue }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(ulong.MaxValue, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_DoubleBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 26,
            ReplySerial = 1,
            Body = new object[] { 3.14159265358979 }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(3.14159265358979, result.Body[0]);
    }

    [Fact]
    public void RoundTrip_ObjectPathBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 27,
            ReplySerial = 1,
            Body = new object[] { new DBusObjectPath("/org/freedesktop/DBus") }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(new DBusObjectPath("/org/freedesktop/DBus"), result.Body[0]);
    }

    [Fact]
    public void RoundTrip_SignatureBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 28,
            ReplySerial = 1,
            Body = new object[] { new DBusSignature("a{sv}") }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(new DBusSignature("a{sv}"), result.Body[0]);
    }

    [Fact]
    public void RoundTrip_VariantBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 29,
            ReplySerial = 1,
            Body = new object[] { new DBusVariant("hello"), new DBusVariant(42u) }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(2, result.Body.Count);
        var v1 = Assert.IsType<DBusVariant>(result.Body[0]);
        Assert.Equal("hello", v1.Value);
        var v2 = Assert.IsType<DBusVariant>(result.Body[1]);
        Assert.Equal(42u, v2.Value);
    }

    [Fact]
    public void RoundTrip_StructBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 30,
            ReplySerial = 1,
            Body = new object[] { new DBusStruct("Alice", 30, true) }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var s = Assert.IsType<DBusStruct>(result.Body[0]);
        Assert.Equal(3, s.Count);
        Assert.Equal("Alice", s[0]);
        Assert.Equal(30, s[1]);
        Assert.Equal(true, s[2]);
    }

    // --- Sender header field ---

    [Fact]
    public void RoundTrip_SenderPreserved()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.Signal,
            Serial = 40,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Tick"
        };
        msg.Sender = ":1.100";

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(":1.100", result.Sender);
    }

    // --- Mixed body types ---

    [Fact]
    public void RoundTrip_MixedBodyTypes()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 50,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Mixed",
            Destination = "com.example.Service",
            Body = new object[] { "text", 42, true, 3.14 }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(4, result.Body.Count);
        Assert.Equal("text", result.Body[0]);
        Assert.Equal(42, result.Body[1]);
        Assert.Equal(true, result.Body[2]);
        Assert.Equal(3.14, result.Body[3]);
    }

    // --- Empty body round-trip ---

    [Fact]
    public void RoundTrip_MethodReturn_EmptyBody()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 60,
            ReplySerial = 50
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodReturn, result.Type);
        Assert.Equal(60u, result.Serial);
        Assert.Equal(50u, result.ReplySerial);
        Assert.Empty(result.Body);
    }

    // --- UInt32 body (not to be confused with ReplySerial) ---

    [Fact]
    public void RoundTrip_UInt32Body()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 70,
            ReplySerial = 1,
            Body = new object[] { 12345u }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        Assert.Equal(12345u, result.Body[0]);
    }

    // --- List<int> body ---

    [Fact]
    public void RoundTrip_ListOfInts()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 80,
            ReplySerial = 1,
            Body = new object[] { list }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultList = Assert.IsType<List<int>>(result.Body[0]);
        Assert.Equal(new List<int> { 1, 2, 3, 4, 5 }, resultList);
    }

    // --- Dictionary<string, string> ---

    [Fact]
    public void RoundTrip_DictionaryStringString()
    {
        var dict = new Dictionary<string, string>
        {
            ["key1"] = "value1",
            ["key2"] = "value2"
        };
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 90,
            ReplySerial = 1,
            Body = new object[] { dict }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var resultDict = Assert.IsType<Dictionary<string, string>>(result.Body[0]);
        Assert.Equal("value1", resultDict["key1"]);
        Assert.Equal("value2", resultDict["key2"]);
    }

    // --- Nested variant containing a list ---

    [Fact]
    public void RoundTrip_VariantContainingList()
    {
        var innerList = new List<string> { "x", "y", "z" };
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 100,
            ReplySerial = 1,
            Body = new object[] { new DBusVariant(innerList) }
        };

        var serialized = _serializer.Serialize(msg);
        var result = _serializer.Deserialize(serialized);

        Assert.Single(result.Body);
        var v = Assert.IsType<DBusVariant>(result.Body[0]);
        var resultList = Assert.IsType<List<string>>(v.Value);
        Assert.Equal(new List<string> { "x", "y", "z" }, resultList);
    }

    // ========================================================================
    // Big-endian deserialization tests
    // ========================================================================

    /// <summary>
    /// Helper to build a big-endian D-Bus message manually.
    /// </summary>
    private static byte[] BuildBigEndianMessage(
        DBusMessageType type, uint serial, byte flags,
        Action<MemoryStream> writeHeaderFields,
        byte[]? bodyBytes = null)
    {
        bodyBytes ??= [];
        var ms = new MemoryStream();

        // Fixed header (12 bytes)
        ms.WriteByte((byte)'B'); // big-endian
        ms.WriteByte((byte)type);
        ms.WriteByte(flags);
        ms.WriteByte(1); // protocol version

        var buf4 = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf4, (uint)bodyBytes.Length);
        ms.Write(buf4); // body length

        BinaryPrimitives.WriteUInt32BigEndian(buf4, serial);
        ms.Write(buf4); // serial

        // Header fields array — write to temp stream to get length
        var fieldsMs = new MemoryStream();
        writeHeaderFields(fieldsMs);
        var fieldsBytes = fieldsMs.ToArray();

        BinaryPrimitives.WriteUInt32BigEndian(buf4, (uint)fieldsBytes.Length);
        ms.Write(buf4); // array byte length
        ms.Write(fieldsBytes);

        // Pad to 8-byte boundary
        while (ms.Position % 8 != 0)
            ms.WriteByte(0);

        ms.Write(bodyBytes);
        return ms.ToArray();
    }

    /// <summary>
    /// Writes a BE header field: struct { byte code, variant { signature, value } }
    /// </summary>
    private static void WriteBeHeaderFieldString(MemoryStream ms, byte fieldCode, string sigChar, string value)
    {
        // Pad struct to 8
        while (ms.Position % 8 != 0)
            ms.WriteByte(0);

        ms.WriteByte(fieldCode);

        // Variant: signature + value
        ms.WriteByte(1); // sig length
        ms.Write(Encoding.ASCII.GetBytes(sigChar));
        ms.WriteByte(0); // sig null

        if (sigChar == "o" || sigChar == "s")
        {
            // Pad to 4 for uint32 string length
            while (ms.Position % 4 != 0)
                ms.WriteByte(0);

            var utf8 = Encoding.UTF8.GetBytes(value);
            var buf = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)utf8.Length);
            ms.Write(buf);
            ms.Write(utf8);
            ms.WriteByte(0); // null terminator
        }
        else if (sigChar == "g")
        {
            var ascii = Encoding.ASCII.GetBytes(value);
            ms.WriteByte((byte)ascii.Length);
            ms.Write(ascii);
            ms.WriteByte(0);
        }
    }

    private static void WriteBeHeaderFieldUInt32(MemoryStream ms, byte fieldCode, uint value)
    {
        while (ms.Position % 8 != 0)
            ms.WriteByte(0);

        ms.WriteByte(fieldCode);

        ms.WriteByte(1); // sig length
        ms.WriteByte((byte)'u');
        ms.WriteByte(0); // sig null

        // Pad to 4 for uint32
        while (ms.Position % 4 != 0)
            ms.WriteByte(0);

        var buf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf, value);
        ms.Write(buf);
    }

    [Fact]
    public void Deserialize_BigEndian_MethodCall_NoBody()
    {
        var msgBytes = BuildBigEndianMessage(
            DBusMessageType.MethodCall, serial: 1, flags: 0,
            writeHeaderFields: fields =>
            {
                WriteBeHeaderFieldString(fields, 1, "o", "/org/freedesktop/DBus"); // PATH
                WriteBeHeaderFieldString(fields, 2, "s", "org.freedesktop.DBus");  // INTERFACE
                WriteBeHeaderFieldString(fields, 3, "s", "Hello");                 // MEMBER
                WriteBeHeaderFieldString(fields, 6, "s", "org.freedesktop.DBus");  // DESTINATION
            });

        var serialized = new DBusSerializedMessage(msgBytes, []);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodCall, result.Type);
        Assert.Equal(1u, result.Serial);
        Assert.Equal("/org/freedesktop/DBus", result.Path?.Value);
        Assert.Equal("org.freedesktop.DBus", result.Interface);
        Assert.Equal("Hello", result.Member);
        Assert.Equal("org.freedesktop.DBus", result.Destination);
        Assert.Empty(result.Body);
    }

    [Fact]
    public void Deserialize_BigEndian_MethodReturn_WithUInt32Body()
    {
        // Body: a single uint32 = 12345
        var bodyBuf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bodyBuf, 12345);

        var msgBytes = BuildBigEndianMessage(
            DBusMessageType.MethodReturn, serial: 2, flags: 0,
            writeHeaderFields: fields =>
            {
                WriteBeHeaderFieldUInt32(fields, 5, 1); // REPLY_SERIAL
                WriteBeHeaderFieldString(fields, 8, "g", "u"); // SIGNATURE
            },
            bodyBytes: bodyBuf);

        var serialized = new DBusSerializedMessage(msgBytes, []);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodReturn, result.Type);
        Assert.Equal(2u, result.Serial);
        Assert.Equal(1u, result.ReplySerial);
        Assert.Single(result.Body);
        Assert.Equal(12345u, result.Body[0]);
    }

    [Fact]
    public void Deserialize_BigEndian_MethodReturn_WithStringBody()
    {
        // Body: a single string "hello"
        var bodyMs = new MemoryStream();
        var utf8 = Encoding.UTF8.GetBytes("hello");
        var lenBuf = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBuf, (uint)utf8.Length);
        bodyMs.Write(lenBuf);
        bodyMs.Write(utf8);
        bodyMs.WriteByte(0); // null terminator
        var bodyBuf = bodyMs.ToArray();

        var msgBytes = BuildBigEndianMessage(
            DBusMessageType.MethodReturn, serial: 3, flags: 0,
            writeHeaderFields: fields =>
            {
                WriteBeHeaderFieldUInt32(fields, 5, 1); // REPLY_SERIAL
                WriteBeHeaderFieldString(fields, 8, "g", "s"); // SIGNATURE
            },
            bodyBytes: bodyBuf);

        var serialized = new DBusSerializedMessage(msgBytes, []);
        var result = _serializer.Deserialize(serialized);

        Assert.Equal(DBusMessageType.MethodReturn, result.Type);
        Assert.Single(result.Body);
        Assert.Equal("hello", result.Body[0]);
    }

    [Fact]
    public void Deserialize_InvalidEndiannessByte_Throws()
    {
        var data = new byte[16];
        data[0] = (byte)'x'; // invalid endianness
        var serialized = new DBusSerializedMessage(data, []);

        var ex = Assert.Throws<NotSupportedException>(() => _serializer.Deserialize(serialized));
        Assert.Contains("0x78", ex.Message); // 'x' = 0x78
    }

    [Fact]
    public void Deserialize_HeaderArrayLengthOutOfBounds_ThrowsInvalidDataException()
    {
        var data = new byte[16];
        data[0] = (byte)'l'; // endianness
        data[1] = (byte)DBusMessageType.MethodCall;
        data[3] = 1; // protocol version
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8, 4), 1); // serial
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(12, 4), uint.MaxValue); // array len

        var serialized = new DBusSerializedMessage(data, []);
        Assert.Throws<InvalidDataException>(() => _serializer.Deserialize(serialized));
    }

    [Fact]
    public void Deserialize_BodyLengthMismatch_ThrowsInvalidDataException()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 1,
            ReplySerial = 1,
            Body = new object[] { "hello" }
        };

        var serialized = _serializer.Serialize(msg);
        var bytes = (byte[])serialized.Message.Clone();

        // Corrupt declared body length to zero while body/signature are still present.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4, 4), 0);

        Assert.Throws<InvalidDataException>(() => _serializer.Deserialize(new DBusSerializedMessage(bytes, serialized.Fds)));
    }

    [Fact]
    public void Deserialize_TrailingBytesAfterBody_ThrowsInvalidDataException()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodReturn,
            Serial = 1,
            ReplySerial = 1,
            Body = new object[] { 42u }
        };

        var serialized = _serializer.Serialize(msg);
        var bytesWithTrailing = new byte[serialized.Message.Length + 1];
        Array.Copy(serialized.Message, bytesWithTrailing, serialized.Message.Length);
        bytesWithTrailing[^1] = 0x00;

        Assert.Throws<InvalidDataException>(() => _serializer.Deserialize(new DBusSerializedMessage(bytesWithTrailing, serialized.Fds)));
    }

    [Fact]
    public void Deserialize_UnixFdIndexOutOfRange_ThrowsInvalidDataException()
    {
        var msg = new DBusMessage
        {
            Type = DBusMessageType.MethodCall,
            Serial = 10,
            Path = new DBusObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "SendFd",
            Body = new object[] { new DBusUnixFd(42) }
        };

        var serialized = _serializer.Serialize(msg);

        // Simulate transport that forgot to carry SCM_RIGHTS.
        Assert.Throws<InvalidDataException>(() => _serializer.Deserialize(new DBusSerializedMessage(serialized.Message, [])));
    }
}
