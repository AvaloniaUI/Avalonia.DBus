using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using NDesk.DBus;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class MessageTests
{
    private readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();
    private readonly IMessageMarshaller _marshaller;

    public MessageTests()
    {
        _marshaller = _factory.CreateMarshaller();
    }

    /// <summary>
    /// Helper: append args and update the message's Signature field via the interface.
    /// </summary>
    private static void AppendTypedArg(IMessageBuilder msg, Action<IWireWriter> writeAction, string sigToAppend)
    {
        msg.AppendArgs(writeAction);
        msg.SetSignature(msg.GetSignature() + sigToAppend);
    }

    /// <summary>
    /// Create a standard method-call message builder for tests.
    /// </summary>
    private IMessageBuilder CreateMethodCall(
        string dest = "org.example.Dest",
        string path = "/org/example/Object",
        string iface = "org.example.Iface",
        string member = "TestMethod")
    {
        var msg = _factory.CreateMessageBuilder();
        msg.SetMessageType(DBusMessageType.MethodCall);
        msg.SetDestination(dest);
        msg.SetPath(path);
        msg.SetInterface(iface);
        msg.SetMember(member);
        return msg;
    }

    [Fact]
    public void CreateMethodCall_DefaultInputs_SetsExpectedHeaderFields()
    {
        var msg = CreateMethodCall();
        Assert.Equal("org.example.Dest", msg.GetDestination());
        Assert.Equal("/org/example/Object", msg.GetPath());
        Assert.Equal("org.example.Iface", msg.GetInterface());
        Assert.Equal("TestMethod", msg.GetMember());
    }

    [Fact]
    public void GetDestination_DefaultMethodCall_ReturnsExpectedDestination()
    {
        var msg = CreateMethodCall();
        Assert.Equal("org.example.Dest", msg.GetDestination());
    }

    [Fact]
    public void CreateMethodCall_DefaultInputs_SetsMethodCallType()
    {
        var msg = CreateMethodCall();
        Assert.Equal(MessageType.MethodCall, ((NDeskMessageBuilder)msg).Message.Header.MessageType);
    }
 
    [Fact]
    public void AppendArgInt16_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        short testValue = -123;
        AppendTypedArg(msg, w => w.WriteInt16(testValue), "n");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadInt16());
    }

    [Fact]
    public void AppendArgUInt16_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        ushort testValue = 456;
        AppendTypedArg(msg, w => w.WriteUInt16(testValue), "q");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadUInt16());
    }

    [Fact]
    public void AppendArgInt32_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        var testValue = -0x12345;
        AppendTypedArg(msg, w => w.WriteInt32(testValue), "i");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadInt32());
    }

    [Fact]
    public void AppendArgUInt32_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        uint testValue = 0x12345678;
        AppendTypedArg(msg, w => w.WriteUInt32(testValue), "u");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadUInt32());
    }

    [Fact]
    public void AppendArgInt64_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        var testValue = -0x123456789L;
        AppendTypedArg(msg, w => w.WriteInt64(testValue), "x");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadInt64());
    }

    [Fact]
    public void AppendArgUInt64_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        var testValue = 0x123456789ABCDEF0UL;
        AppendTypedArg(msg, w => w.WriteUInt64(testValue), "t");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadUInt64());
    }

    [Fact]
    public void AppendArgString_MethodCallBody_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        var testValue = "Hello World";
        AppendTypedArg(msg, w => w.WriteString(testValue), "s");

        var reader = msg.GetBodyReader();
        Assert.Equal(testValue, reader.ReadString());
    }

    [Fact]
    public void AppendArgDouble_MethodCallBody_ReadsBackSameBitPattern()
    {
        var msg = CreateMethodCall();
        var testValue = 3.14159265;
        AppendTypedArg(msg, w => w.WriteDouble(testValue), "d");

        var reader = msg.GetBodyReader();
        Assert.Equal(BitConverter.DoubleToInt64Bits(testValue),
                     BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
    }

    [Fact]
    public void AppendArgBoolean_TrueValue_ReadsBackTrue()
    {
        var msg = CreateMethodCall();
        AppendTypedArg(msg, w => w.WriteBoolean(true), "b");

        var reader = msg.GetBodyReader();
        Assert.True(reader.ReadBoolean());
    }

    [Fact]
    public void AppendArgByte_ValueAB_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        AppendTypedArg(msg, w => w.WriteByte(0xAB), "y");

        var reader = msg.GetBodyReader();
        Assert.Equal(0xAB, reader.ReadByte());
    }

    [Fact]
    public void AppendArgByte_ValueCD_ReadsBackSameValue()
    {
        var msg = CreateMethodCall();
        AppendTypedArg(msg, w => w.WriteByte(0xCD), "y");

        var reader = msg.GetBodyReader();
        Assert.Equal(0xCD, reader.ReadByte());
    }
 
    [Fact]
    public void AppendArgArrayUInt32_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new uint[] { 1, 2, 3, 4 };
        AppendTypedArg(msg, w => w.WriteArray(values), "au");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<uint>());
    }

    [Fact]
    public void AppendArgArrayInt32_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new[] { -100, 0, 100, int.MaxValue };
        AppendTypedArg(msg, w => w.WriteArray(values), "ai");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<int>());
    }

    [Fact]
    public void AppendArgArrayUInt64_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new[] { 0UL, 1UL, ulong.MaxValue, 0x123456789ABCDEF0UL };
        AppendTypedArg(msg, w => w.WriteArray(values), "at");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<ulong>());
    }

    [Fact]
    public void AppendArgArrayInt64_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new[] { long.MinValue, -1L, 0L, long.MaxValue };
        AppendTypedArg(msg, w => w.WriteArray(values), "ax");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<long>());
    }

    [Fact]
    public void AppendArgArrayDouble_MethodCallBody_ReadsBackSameBitPatterns()
    {
        var msg = CreateMethodCall();
        var values = new[] { 1.1, 2.2, 3.3 };
        AppendTypedArg(msg, w => w.WriteArray(values), "ad");

        var reader = msg.GetBodyReader();
        var result = reader.ReadArray<double>();
        Assert.Equal(values.Length, result.Length);
        for (var i = 0; i < values.Length; i++)
            Assert.Equal(BitConverter.DoubleToInt64Bits(values[i]),
                         BitConverter.DoubleToInt64Bits(result[i]));
    }

    [Fact]
    public void AppendArgArrayByte_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new byte[] { 0x01, 0x02, 0xFF, 0x00 };
        AppendTypedArg(msg, w => w.WriteArray(values), "ay");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<byte>());
    }

    [Fact]
    public void AppendArgArrayBoolean_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new[] { true, false, true, false, true };
        AppendTypedArg(msg, w => w.WriteArray(values), "ab");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<bool>());
    }

    [Fact]
    public void AppendArgArrayString_MethodCallBody_ReadsBackSameValues()
    {
        var msg = CreateMethodCall();
        var values = new[] { "hello", "world", "", "test" };
        AppendTypedArg(msg, w => w.WriteArray(values), "as");

        var reader = msg.GetBodyReader();
        Assert.Equal(values, reader.ReadArray<string>());
    }
 
    [Fact]
    public void MarshalDemarshal_MessageWithHeadersAndArg_PreservesMessageFields()
    {
        var original = CreateMethodCall();
        original.SetSerial(9999);
        original.SetSender("org.sender.Test");
        original.SetReplySerial(1111);
        AppendTypedArg(original, w => w.WriteUInt32(42), "u");

        // Marshal and demarshal to create a copy
        var bytes = _marshaller.Marshal(original);
        var copy = _marshaller.Demarshal(bytes);

        Assert.Equal(original.GetPath(), copy.GetPath());
        Assert.Equal(original.GetInterface(), copy.GetInterface());
        Assert.Equal(original.GetMember(), copy.GetMember());
        Assert.Equal(original.GetDestination(), copy.GetDestination());
        Assert.Equal(original.GetSender(), copy.GetSender());
        Assert.Equal(original.GetSerial(), copy.GetSerial());
        Assert.Equal(original.GetReplySerial(), copy.GetReplySerial());
        Assert.Equal(original.GetSignature(), copy.GetSignature());
    }

    [Fact]
    public void MarshalDemarshal_MessageWithMixedArgs_PreservesSignatureAndBodyValues()
    {
        var original = CreateMethodCall();
        original.SetSerial(42);
        // Write all args in a single call so alignment is correct across types
        original.AppendArgs(w =>
        {
            w.WriteInt32(-100);
            w.WriteString("test");
            w.WriteUInt64(0xDEADBEEFUL);
        });
        original.SetSignature("ist");

        var bytes = _marshaller.Marshal(original);
        var restored = _marshaller.Demarshal(bytes);

        Assert.Equal("ist", restored.GetSignature());

        var reader = restored.GetBodyReader();
        Assert.Equal(-100, reader.ReadInt32());
        Assert.Equal("test", reader.ReadString());
        Assert.Equal(0xDEADBEEFUL, reader.ReadUInt64());
    }

    [Fact]
    public void Demarshal_ShortInvalidPayload_ThrowsArgumentException()
    {
        var shortData = new byte[] { 0x6C, 0x01, 0x00, 0x01, 0x00, 0x00 };
        Assert.Throws<ArgumentException>(() => _marshaller.Demarshal(shortData));
    }

    [Fact]
    public void Demarshal_EmptyPayload_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _marshaller.Demarshal([]));
    }

    [Fact]
    public void BytesNeeded_EmptyPayload_ReturnsZero()
    {
        Assert.Equal(0, _marshaller.BytesNeeded([], 0));
    }

    [Fact]
    public void BytesNeeded_GarbagePayload_ReturnsMinusOne()
    {
        var garbage = new byte[16];
        Array.Fill(garbage, (byte)0xFF);
        Assert.Equal(-1, _marshaller.BytesNeeded(garbage, 16));
    }

    [Fact]
    public void MarshalDemarshal_MessageWithCustomInterface_PreservesInterface()
    {
        var original = CreateMethodCall(iface: "org.copy.Iface");
        original.SetSerial(100);
        AppendTypedArg(original, w => w.WriteUInt32(1), "u");

        var bytes = _marshaller.Marshal(original);
        var copy = _marshaller.Demarshal(bytes);

        Assert.Equal("org.copy.Iface", copy.GetInterface());
    }

    [Fact]
    public void MarshalDemarshal_MessageWithCustomMember_PreservesMember()
    {
        var original = CreateMethodCall(member: "CopiedMethod");
        original.SetSerial(101);
        AppendTypedArg(original, w => w.WriteUInt32(2), "u");

        var bytes = _marshaller.Marshal(original);
        var copy = _marshaller.Demarshal(bytes);

        Assert.Equal("CopiedMethod", copy.GetMember());
    }

    [Fact]
    public void MarshalDemarshal_MessageWithReplySerial_PreservesReplySerial()
    {
        var original = CreateMethodCall();
        original.SetSerial(102);
        original.SetReplySerial(7777);
        AppendTypedArg(original, w => w.WriteUInt32(3), "u");

        var bytes = _marshaller.Marshal(original);
        var copy = _marshaller.Demarshal(bytes);

        Assert.Equal(7777u, copy.GetReplySerial());
    }

    [Fact]
    public void MarshalDemarshal_MessageWithMultipleArgs_PreservesBodyArguments()
    {
        var original = CreateMethodCall();
        original.SetSerial(103);
        AppendTypedArg(original, w =>
        {
            w.WriteUInt32(0xCAFEBABE);
            w.WriteString("copied");
        }, "us");

        var bytes = _marshaller.Marshal(original);
        var copy = _marshaller.Demarshal(bytes);

        var reader = copy.GetBodyReader();
        Assert.Equal(0xCAFEBABEu, reader.ReadUInt32());
        Assert.Equal("copied", reader.ReadString());
    }

    [Fact]
    public void MarshalDemarshal_FullyPopulatedMessage_PreservesHeaderAndBody()
    {
        var original = CreateMethodCall(
            dest: "org.verify.Dest",
            path: "/org/verify/Path",
            iface: "org.verify.Iface",
            member: "VerifyMethod");
        original.SetSerial(555);
        original.SetReplySerial(666);
        original.SetSender("org.verify.Sender");
        original.AppendArgs(w =>
        {
            w.WriteInt32(42);
            w.WriteString("verified");
            w.WriteBoolean(true);
            w.WriteDouble(2.718);
        });
        original.SetSignature("isbd");

        var bytes = _marshaller.Marshal(original);
        var restored = _marshaller.Demarshal(bytes);

        // Verify header fields
        Assert.Equal("org.verify.Dest", restored.GetDestination());
        Assert.Equal("/org/verify/Path", restored.GetPath());
        Assert.Equal("org.verify.Iface", restored.GetInterface());
        Assert.Equal("VerifyMethod", restored.GetMember());
        Assert.Equal(555u, restored.GetSerial());
        Assert.Equal(666u, restored.GetReplySerial());
        Assert.Equal("org.verify.Sender", restored.GetSender());
        Assert.Equal("isbd", restored.GetSignature());

        // Verify body args
        var reader = restored.GetBodyReader();
        Assert.Equal(42, reader.ReadInt32());
        Assert.Equal("verified", reader.ReadString());
        Assert.True(reader.ReadBoolean());
        Assert.Equal(BitConverter.DoubleToInt64Bits(2.718),
                     BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
    }

}
