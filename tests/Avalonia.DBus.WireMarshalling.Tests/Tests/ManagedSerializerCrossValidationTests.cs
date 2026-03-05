extern alias managed;

using NDesk.DBus;
using Xunit;

using ManagedSerializer = managed::Avalonia.DBus.Managed.ManagedDBusMessageSerializer;
using ManagedMessage = managed::Avalonia.DBus.DBusMessage;
using ManagedMessageType = managed::Avalonia.DBus.DBusMessageType;
using ManagedMessageFlags = managed::Avalonia.DBus.DBusMessageFlags;
using ManagedObjectPath = managed::Avalonia.DBus.DBusObjectPath;
using ManagedSerializedMessage = managed::Avalonia.DBus.DBusSerializedMessage;
using NDeskMessage = NDesk.DBus.Message;
using NDeskMessageType = NDesk.DBus.MessageType;
using NDeskSignature = NDesk.DBus.Signature;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

/// <summary>
/// Cross-validates the managed <see cref="ManagedSerializer"/> against the
/// NDesk reference implementation to ensure byte-level wire compatibility.
/// </summary>
public class ManagedSerializerCrossValidationTests
{
    private readonly ManagedSerializer _serializer = new();

    // -------------------------------------------------------------------
    // Test 1: Managed Serialize -> NDesk Demarshal
    // -------------------------------------------------------------------

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_MethodCall_NoBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 1,
            Path = new ManagedObjectPath("/org/freedesktop/DBus"),
            Interface = "org.freedesktop.DBus",
            Member = "Hello",
            Destination = "org.freedesktop.DBus"
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.MethodCall, ndeskMsg.Header.MessageType);
        Assert.Equal(1u, ndeskMsg.Header.Serial);
        Assert.Equal("/org/freedesktop/DBus", GetNDeskFieldString(ndeskMsg, FieldCode.Path));
        Assert.Equal("org.freedesktop.DBus", GetNDeskFieldString(ndeskMsg, FieldCode.Interface));
        Assert.Equal("Hello", GetNDeskFieldString(ndeskMsg, FieldCode.Member));
        Assert.Equal("org.freedesktop.DBus", GetNDeskFieldString(ndeskMsg, FieldCode.Destination));
        Assert.Null(ndeskMsg.Body);
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_MethodCall_StringBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 2,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Echo",
            Destination = "com.example.Service",
            Body = new object[] { "hello world" }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.MethodCall, ndeskMsg.Header.MessageType);
        Assert.Equal(2u, ndeskMsg.Header.Serial);
        Assert.Equal("s", ndeskMsg.Signature.Value);

        // Read body using NDesk reader
        var reader = new MessageReader(ndeskMsg);
        var bodyStr = reader.ReadString();
        Assert.Equal("hello world", bodyStr);
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_MethodCall_MultipleIntArgs()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 3,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Add",
            Destination = "com.example.Service",
            Body = new object[] { 42, 58 }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal("ii", ndeskMsg.Signature.Value);

        var reader = new MessageReader(ndeskMsg);
        Assert.Equal(42, reader.ReadInt32());
        Assert.Equal(58, reader.ReadInt32());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_Signal()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.Signal,
            Serial = 10,
            Path = new ManagedObjectPath("/org/example"),
            Interface = "org.example.Signals",
            Member = "SomethingHappened",
            Body = new object[] { "event data", 42u }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.Signal, ndeskMsg.Header.MessageType);
        Assert.Equal(10u, ndeskMsg.Header.Serial);
        Assert.Equal("/org/example", GetNDeskFieldString(ndeskMsg, FieldCode.Path));
        Assert.Equal("org.example.Signals", GetNDeskFieldString(ndeskMsg, FieldCode.Interface));
        Assert.Equal("SomethingHappened", GetNDeskFieldString(ndeskMsg, FieldCode.Member));
        Assert.Equal("su", ndeskMsg.Signature.Value);

        var reader = new MessageReader(ndeskMsg);
        Assert.Equal("event data", reader.ReadString());
        Assert.Equal(42u, reader.ReadUInt32());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_MethodReturn_WithReplySerial()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodReturn,
            Serial = 5,
            ReplySerial = 3,
            Destination = ":1.42",
            Body = new object[] { "result" }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.MethodReturn, ndeskMsg.Header.MessageType);
        Assert.Equal(5u, ndeskMsg.Header.Serial);
        Assert.Equal(3u, (uint)ndeskMsg.Header.Fields[FieldCode.ReplySerial]);
        Assert.Equal(":1.42", GetNDeskFieldString(ndeskMsg, FieldCode.Destination));

        var reader = new MessageReader(ndeskMsg);
        Assert.Equal("result", reader.ReadString());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_Error()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.Error,
            Serial = 6,
            ReplySerial = 3,
            ErrorName = "org.freedesktop.DBus.Error.UnknownMethod",
            Destination = ":1.42",
            Body = new object[] { "Method not found" }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.Error, ndeskMsg.Header.MessageType);
        Assert.Equal(6u, ndeskMsg.Header.Serial);
        Assert.Equal(3u, (uint)ndeskMsg.Header.Fields[FieldCode.ReplySerial]);
        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod",
            GetNDeskFieldString(ndeskMsg, FieldCode.ErrorName));
        Assert.Equal(":1.42", GetNDeskFieldString(ndeskMsg, FieldCode.Destination));

        var reader = new MessageReader(ndeskMsg);
        Assert.Equal("Method not found", reader.ReadString());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_Flags_NoReplyExpected()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 20,
            Flags = ManagedMessageFlags.NoReplyExpected,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Fire",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.True((ndeskMsg.Header.Flags & HeaderFlag.NoReplyExpected) != 0);
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_Flags_NoAutoStart()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 21,
            Flags = ManagedMessageFlags.NoAutoStart,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Ping",
            Destination = "com.example.Service"
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.True((ndeskMsg.Header.Flags & HeaderFlag.NoAutoStart) != 0);
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_SenderField()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.Signal,
            Serial = 30,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Tick",
        };
        msg.Sender = ":1.100";

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(":1.100", GetNDeskFieldString(ndeskMsg, FieldCode.Sender));
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_BooleanBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodReturn,
            Serial = 40,
            ReplySerial = 1,
            Body = new object[] { true, false }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal("bb", ndeskMsg.Signature.Value);
        var reader = new MessageReader(ndeskMsg);
        Assert.True(reader.ReadBoolean());
        Assert.False(reader.ReadBoolean());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_UInt64Body()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodReturn,
            Serial = 41,
            ReplySerial = 1,
            Body = new object[] { ulong.MaxValue }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal("t", ndeskMsg.Signature.Value);
        var reader = new MessageReader(ndeskMsg);
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
    }

    [Fact]
    public void ManagedSerialize_NDeskDemarshal_DoubleBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodReturn,
            Serial = 42,
            ReplySerial = 1,
            Body = new object[] { 3.14159265358979 }
        };

        var serialized = _serializer.Serialize(msg);
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        Assert.Equal("d", ndeskMsg.Signature.Value);
        var reader = new MessageReader(ndeskMsg);
        Assert.Equal(3.14159265358979, reader.ReadDouble());
    }

    // -------------------------------------------------------------------
    // Test 2: NDesk Marshal -> Managed Deserialize
    // -------------------------------------------------------------------

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_MethodCall_NoBody()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodCall;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 1;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/org/freedesktop/DBus");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "org.freedesktop.DBus";
        ndeskMsg.Header.Fields[FieldCode.Member] = "Hello";
        ndeskMsg.Header.Fields[FieldCode.Destination] = "org.freedesktop.DBus";

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(ManagedMessageType.MethodCall, result.Type);
        Assert.Equal(1u, result.Serial);
        Assert.Equal("/org/freedesktop/DBus", result.Path?.Value);
        Assert.Equal("org.freedesktop.DBus", result.Interface);
        Assert.Equal("Hello", result.Member);
        Assert.Equal("org.freedesktop.DBus", result.Destination);
        Assert.Empty(result.Body);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_MethodCall_StringBody()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodCall;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 2;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/test");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "com.example.Test";
        ndeskMsg.Header.Fields[FieldCode.Member] = "Echo";
        ndeskMsg.Header.Fields[FieldCode.Destination] = "com.example.Service";
        ndeskMsg.Signature = new NDeskSignature("s");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("hello world");
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(ManagedMessageType.MethodCall, result.Type);
        Assert.Equal(2u, result.Serial);
        Assert.Equal("/test", result.Path?.Value);
        Assert.Equal("com.example.Test", result.Interface);
        Assert.Equal("Echo", result.Member);
        Assert.Equal("com.example.Service", result.Destination);
        Assert.Single(result.Body);
        Assert.Equal("hello world", result.Body[0]);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_MethodCall_MultipleIntArgs()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodCall;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 3;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/test");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "com.example.Test";
        ndeskMsg.Header.Fields[FieldCode.Member] = "Add";
        ndeskMsg.Header.Fields[FieldCode.Destination] = "com.example.Service";
        ndeskMsg.Signature = new NDeskSignature("ii");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write(42);
        writer.Write(58);
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(2, result.Body.Count);
        Assert.Equal(42, result.Body[0]);
        Assert.Equal(58, result.Body[1]);
        Assert.Equal("ii", result.Signature.Value);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_Signal()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.Signal;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 10;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/org/example");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "org.example.Signals";
        ndeskMsg.Header.Fields[FieldCode.Member] = "SomethingHappened";
        ndeskMsg.Signature = new NDeskSignature("su");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("event data");
        writer.Write(42u);
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(ManagedMessageType.Signal, result.Type);
        Assert.Equal(10u, result.Serial);
        Assert.Equal("/org/example", result.Path?.Value);
        Assert.Equal("org.example.Signals", result.Interface);
        Assert.Equal("SomethingHappened", result.Member);
        Assert.Equal(2, result.Body.Count);
        Assert.Equal("event data", result.Body[0]);
        Assert.Equal(42u, result.Body[1]);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_MethodReturn_WithReplySerial()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodReturn;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 5;
        ndeskMsg.Header.Fields[FieldCode.ReplySerial] = 3u;
        ndeskMsg.Header.Fields[FieldCode.Destination] = ":1.42";
        ndeskMsg.Signature = new NDeskSignature("s");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("result");
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(ManagedMessageType.MethodReturn, result.Type);
        Assert.Equal(5u, result.Serial);
        Assert.Equal(3u, result.ReplySerial);
        Assert.Equal(":1.42", result.Destination);
        Assert.Single(result.Body);
        Assert.Equal("result", result.Body[0]);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_Error()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.Error;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 6;
        ndeskMsg.Header.Fields[FieldCode.ReplySerial] = 3u;
        ndeskMsg.Header.Fields[FieldCode.ErrorName] = "org.freedesktop.DBus.Error.UnknownMethod";
        ndeskMsg.Header.Fields[FieldCode.Destination] = ":1.42";
        ndeskMsg.Signature = new NDeskSignature("s");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("Method not found");
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(ManagedMessageType.Error, result.Type);
        Assert.Equal(6u, result.Serial);
        Assert.Equal(3u, result.ReplySerial);
        Assert.Equal("org.freedesktop.DBus.Error.UnknownMethod", result.ErrorName);
        Assert.Equal(":1.42", result.Destination);
        Assert.Single(result.Body);
        Assert.Equal("Method not found", result.Body[0]);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_WithSender()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.Signal;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 30;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/test");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "com.example.Test";
        ndeskMsg.Header.Fields[FieldCode.Member] = "Tick";
        ndeskMsg.Header.Fields[FieldCode.Sender] = ":1.100";

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(":1.100", result.Sender);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_BooleanBody()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodReturn;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 40;
        ndeskMsg.Header.Fields[FieldCode.ReplySerial] = 1u;
        ndeskMsg.Signature = new NDeskSignature("bb");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write(true);
        writer.Write(false);
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Equal(2, result.Body.Count);
        Assert.Equal(true, result.Body[0]);
        Assert.Equal(false, result.Body[1]);
    }

    [Fact]
    public void NDeskMarshal_ManagedDeserialize_DoubleBody()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodReturn;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 42;
        ndeskMsg.Header.Fields[FieldCode.ReplySerial] = 1u;
        ndeskMsg.Signature = new NDeskSignature("d");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write(3.14159265358979);
        ndeskMsg.Body = writer.ToArray();

        var bytes = MessageWire.Marshal(ndeskMsg);
        var result = _serializer.Deserialize(new ManagedSerializedMessage(bytes, []));

        Assert.Single(result.Body);
        Assert.Equal(3.14159265358979, result.Body[0]);
    }

    // -------------------------------------------------------------------
    // Test 3: BytesNeeded compatibility
    // -------------------------------------------------------------------

    [Fact]
    public void BytesNeeded_ManagedSerialized_MatchesNDeskBytesNeeded_NoBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 1,
            Path = new ManagedObjectPath("/org/freedesktop/DBus"),
            Interface = "org.freedesktop.DBus",
            Member = "Hello",
            Destination = "org.freedesktop.DBus"
        };

        var serialized = _serializer.Serialize(msg);
        var fullBytes = serialized.Message;

        // BytesNeeded should return the total length when given at least 16 bytes
        var needed = MessageWire.BytesNeeded(fullBytes, fullBytes.Length);
        Assert.Equal(fullBytes.Length, needed);
    }

    [Fact]
    public void BytesNeeded_ManagedSerialized_MatchesNDeskBytesNeeded_WithBody()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 2,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.example.Test",
            Member = "Echo",
            Destination = "com.example.Service",
            Body = new object[] { "hello world" }
        };

        var serialized = _serializer.Serialize(msg);
        var fullBytes = serialized.Message;

        var needed = MessageWire.BytesNeeded(fullBytes, fullBytes.Length);
        Assert.Equal(fullBytes.Length, needed);
    }

    [Fact]
    public void BytesNeeded_ManagedSerialized_WorksWithPartialHeader()
    {
        var msg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 100,
            Path = new ManagedObjectPath("/test/long/path/for/testing"),
            Interface = "com.example.LongInterface",
            Member = "LongMethod",
            Destination = "com.example.LongDestination",
            Body = new object[] { "test body", 42, true, 3.14 }
        };

        var serialized = _serializer.Serialize(msg);
        var fullBytes = serialized.Message;

        // Provide just the first 16 bytes
        var partial = new byte[16];
        Array.Copy(fullBytes, partial, 16);

        var needed = MessageWire.BytesNeeded(partial, 16);
        Assert.Equal(fullBytes.Length, needed);
    }

    // -------------------------------------------------------------------
    // Test 4: Full round-trip: Managed -> NDesk -> Managed
    // -------------------------------------------------------------------

    [Fact]
    public void FullRoundTrip_ManagedSerialize_NDeskDemarshalAndMarshal_ManagedDeserialize()
    {
        var original = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 99,
            Path = new ManagedObjectPath("/org/test"),
            Interface = "org.test.Iface",
            Member = "DoStuff",
            Destination = "org.test.Service",
            Body = new object[] { "payload", 42 }
        };

        // Managed -> bytes
        var serialized = _serializer.Serialize(original);

        // bytes -> NDesk
        var ndeskMsg = MessageWire.Demarshal(serialized.Message);

        // NDesk -> bytes
        var ndeskBytes = MessageWire.Marshal(ndeskMsg);

        // bytes -> Managed
        var result = _serializer.Deserialize(new ManagedSerializedMessage(ndeskBytes, []));

        Assert.Equal(original.Type, result.Type);
        Assert.Equal(original.Serial, result.Serial);
        Assert.Equal(original.Path?.Value, result.Path?.Value);
        Assert.Equal(original.Interface, result.Interface);
        Assert.Equal(original.Member, result.Member);
        Assert.Equal(original.Destination, result.Destination);
        Assert.Equal(original.Body.Count, result.Body.Count);
        Assert.Equal("payload", result.Body[0]);
        Assert.Equal(42, result.Body[1]);
    }

    [Fact]
    public void FullRoundTrip_NDeskMarshal_ManagedDeserializeAndSerialize_NDeskDemarshal()
    {
        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodCall;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 77;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/org/test");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "org.test.Iface";
        ndeskMsg.Header.Fields[FieldCode.Member] = "DoStuff";
        ndeskMsg.Header.Fields[FieldCode.Destination] = "org.test.Service";
        ndeskMsg.Signature = new NDeskSignature("si");

        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("payload");
        writer.Write(42);
        ndeskMsg.Body = writer.ToArray();

        // NDesk -> bytes
        var ndeskBytes = MessageWire.Marshal(ndeskMsg);

        // bytes -> Managed
        var managed = _serializer.Deserialize(new ManagedSerializedMessage(ndeskBytes, []));

        // Managed -> bytes
        var serialized = _serializer.Serialize(managed);

        // bytes -> NDesk
        var result = MessageWire.Demarshal(serialized.Message);

        Assert.Equal(NDeskMessageType.MethodCall, result.Header.MessageType);
        Assert.Equal(77u, result.Header.Serial);
        Assert.Equal("/org/test", GetNDeskFieldString(result, FieldCode.Path));
        Assert.Equal("org.test.Iface", GetNDeskFieldString(result, FieldCode.Interface));
        Assert.Equal("DoStuff", GetNDeskFieldString(result, FieldCode.Member));
        Assert.Equal("org.test.Service", GetNDeskFieldString(result, FieldCode.Destination));
        Assert.Equal("si", result.Signature.Value);

        var reader2 = new MessageReader(result);
        Assert.Equal("payload", reader2.ReadString());
        Assert.Equal(42, reader2.ReadInt32());
    }

    // -------------------------------------------------------------------
    // Test 5: Byte-level header validation
    // -------------------------------------------------------------------

    [Fact]
    public void ByteLevel_ManagedAndNDesk_ProduceCrossCompatibleMessages()
    {
        // Build same message with both systems
        var managedMsg = new ManagedMessage
        {
            Type = ManagedMessageType.MethodCall,
            Serial = 1,
            Path = new ManagedObjectPath("/test"),
            Interface = "com.test.Iface",
            Member = "Ping",
            Destination = "com.test.Svc"
        };

        var ndeskMsg = new NDeskMessage();
        ndeskMsg.Header.MessageType = NDeskMessageType.MethodCall;
        ndeskMsg.Header.Flags = HeaderFlag.None;
        ndeskMsg.Header.Serial = 1;
        ndeskMsg.Header.Fields[FieldCode.Path] = new ObjectPath("/test");
        ndeskMsg.Header.Fields[FieldCode.Interface] = "com.test.Iface";
        ndeskMsg.Header.Fields[FieldCode.Member] = "Ping";
        ndeskMsg.Header.Fields[FieldCode.Destination] = "com.test.Svc";

        var managedBytes = _serializer.Serialize(managedMsg).Message;
        var ndeskBytes = MessageWire.Marshal(ndeskMsg);

        // Both should be parseable by each other's deserializer
        var ndeskParsed = MessageWire.Demarshal(managedBytes);
        Assert.Equal(NDeskMessageType.MethodCall, ndeskParsed.Header.MessageType);
        Assert.Equal(1u, ndeskParsed.Header.Serial);

        var managedParsed = _serializer.Deserialize(new ManagedSerializedMessage(ndeskBytes, []));
        Assert.Equal(ManagedMessageType.MethodCall, managedParsed.Type);
        Assert.Equal(1u, managedParsed.Serial);

        // The protocol version, endianness, message type, and serial should match
        // in the fixed header (first 12 bytes)
        Assert.Equal(managedBytes[0], ndeskBytes[0]); // endianness: 'l'
        Assert.Equal(managedBytes[1], ndeskBytes[1]); // message type
        // byte[2] = flags (may differ in default flags)
        Assert.Equal(managedBytes[3], ndeskBytes[3]); // protocol version
        // bytes 8-11 = serial
        Assert.Equal(managedBytes[8], ndeskBytes[8]);
        Assert.Equal(managedBytes[9], ndeskBytes[9]);
        Assert.Equal(managedBytes[10], ndeskBytes[10]);
        Assert.Equal(managedBytes[11], ndeskBytes[11]);
    }

    // -------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------

    private static string? GetNDeskFieldString(NDeskMessage msg, FieldCode code)
    {
        if (!msg.Header.Fields.TryGetValue(code, out var val))
            return null;

        if (val is ObjectPath path)
            return path.Value;

        return val as string;
    }
}
