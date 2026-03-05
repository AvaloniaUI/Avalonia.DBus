using System.Collections.Generic;
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
}
