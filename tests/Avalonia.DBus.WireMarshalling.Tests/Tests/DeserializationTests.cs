using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using NDesk.DBus;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class DeserializationTests
{
    private static readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();

    private const uint TestSerial = 0x12345678;
    private const uint TestReplySerial = 0x87654321;
    private const uint TestBodyValue = 0xAABBCCDD;

    private static byte[] BuildTestBlob(EndianFlag endianness)
    {
        var msg = new Message();
        msg.Header.Endianness = endianness;
        msg.Header.MessageType = MessageType.MethodReturn;
        msg.Header.Flags = HeaderFlag.NoReplyExpected;
        msg.Header.MajorVersion = Protocol.Version;
        msg.Header.Serial = TestSerial;
        msg.Header.Fields = new Dictionary<FieldCode, object>
        {
            { FieldCode.ReplySerial, TestReplySerial },
            { FieldCode.Signature, new Signature("u") }
        };

        var bodyWriter = new MessageWriter(endianness);
        bodyWriter.Write(TestBodyValue);
        msg.Body = bodyWriter.ToArray();

        var headerData = msg.GetHeaderData();
        var result = new byte[headerData.Length + msg.Body.Length];
        Buffer.BlockCopy(headerData, 0, result, 0, headerData.Length);
        Buffer.BlockCopy(msg.Body, 0, result, headerData.Length, msg.Body.Length);
        return result;
    }

    private static uint ReadUInt32(byte[] data, int offset, ByteOrder order)
    {
        if (order == ByteOrder.LittleEndian)
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        else
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    private readonly byte[] _leBlob = BuildTestBlob(EndianFlag.Little);
    private readonly byte[] _beBlob = BuildTestBlob(EndianFlag.Big);
    private readonly IMessageMarshaller _marshaller = _factory.CreateMarshaller();

    // Body length from LE blob header
    [Fact]
    public void ReadHeaderBodyLength_LittleEndianBlob_ReturnsExpectedLength()
    {
        var bodyLen = ReadUInt32(_leBlob, 4, ByteOrder.LittleEndian);
        Assert.Equal(4u, bodyLen);
    }

    // Body length from BE blob header
    [Fact]
    public void ReadHeaderBodyLength_BigEndianBlob_ReturnsExpectedLength()
    {
        var bodyLen = ReadUInt32(_beBlob, 4, ByteOrder.BigEndian);
        Assert.Equal(4u, bodyLen);
    }

    // Serial from LE blob header
    [Fact]
    public void ReadHeaderSerial_LittleEndianBlob_ReturnsTestSerial()
    {
        var serial = ReadUInt32(_leBlob, 8, ByteOrder.LittleEndian);
        Assert.Equal(TestSerial, serial);
    }

    // Serial from BE blob header
    [Fact]
    public void ReadHeaderSerial_BigEndianBlob_ReturnsTestSerial()
    {
        var serial = ReadUInt32(_beBlob, 8, ByteOrder.BigEndian);
        Assert.Equal(TestSerial, serial);
    }

    // BytesNeeded for full LE blob returns the message length
    [Fact]
    public void BytesNeeded_LittleEndianFullBlob_ReturnsBlobLength()
    {
        var needed = _marshaller.BytesNeeded(_leBlob, _leBlob.Length);
        Assert.Equal(_leBlob.Length, needed);
    }

    // BytesNeeded for full BE blob returns the message length
    [Fact]
    public void BytesNeeded_BigEndianFullBlob_ReturnsBlobLength()
    {
        var needed = _marshaller.BytesNeeded(_beBlob, _beBlob.Length);
        Assert.Equal(_beBlob.Length, needed);
    }

    // Demarshal LE blob succeeds
    [Fact]
    public void Demarshal_LittleEndianBlob_ReturnsMessage()
    {
        var msg = _marshaller.Demarshal(_leBlob);
        Assert.NotNull(msg);
    }

    // Demarshal BE blob succeeds
    [Fact]
    public void Demarshal_BigEndianBlob_ReturnsMessage()
    {
        var msg = _marshaller.Demarshal(_beBlob);
        Assert.NotNull(msg);
    }

    // Demarshalled LE message serial
    [Fact]
    public void GetSerial_DemarshalledLittleEndianBlob_ReturnsTestSerial()
    {
        var msg = _marshaller.Demarshal(_leBlob);
        Assert.Equal(TestSerial, msg.GetSerial());
    }

    // Demarshalled BE message serial
    [Fact]
    public void GetSerial_DemarshalledBigEndianBlob_ReturnsTestSerial()
    {
        var msg = _marshaller.Demarshal(_beBlob);
        Assert.Equal(TestSerial, msg.GetSerial());
    }

    // Demarshalled LE reply serial
    [Fact]
    public void GetReplySerial_DemarshalledLittleEndianBlob_ReturnsTestReplySerial()
    {
        var msg = _marshaller.Demarshal(_leBlob);
        Assert.Equal(TestReplySerial, msg.GetReplySerial());
    }

    // Demarshalled BE reply serial
    [Fact]
    public void GetReplySerial_DemarshalledBigEndianBlob_ReturnsTestReplySerial()
    {
        var msg = _marshaller.Demarshal(_beBlob);
        Assert.Equal(TestReplySerial, msg.GetReplySerial());
    }

    // Demarshalled LE signature
    [Fact]
    public void GetSignature_DemarshalledLittleEndianBlob_ReturnsUInt32Signature()
    {
        var msg = _marshaller.Demarshal(_leBlob);
        Assert.Equal("u", msg.GetSignature());
    }

    // Demarshalled BE signature
    [Fact]
    public void GetSignature_DemarshalledBigEndianBlob_ReturnsUInt32Signature()
    {
        var msg = _marshaller.Demarshal(_beBlob);
        Assert.Equal("u", msg.GetSignature());
    }

    // BytesNeeded with various partial lengths for LE
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(12, 0)]
    [InlineData(15, 0)]
    public void BytesNeeded_LittleEndianPartialBlob_ReturnsZero(int partialLen, int expected)
    {
        var partial = new byte[partialLen];
        if (partialLen > 0)
            Buffer.BlockCopy(_leBlob, 0, partial, 0, Math.Min(partialLen, _leBlob.Length));
        Assert.Equal(expected, _marshaller.BytesNeeded(partial, partialLen));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(32)]
    public void BytesNeeded_LittleEndianSufficientData_ReturnsFullLength(int partialLen)
    {
        var partial = new byte[partialLen];
        Buffer.BlockCopy(_leBlob, 0, partial, 0, Math.Min(partialLen, _leBlob.Length));
        Assert.Equal(_leBlob.Length, _marshaller.BytesNeeded(partial, partialLen));
    }

    // BytesNeeded with various partial lengths for BE
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(4, 0)]
    [InlineData(12, 0)]
    [InlineData(15, 0)]
    public void BytesNeeded_BigEndianPartialBlob_ReturnsZero(int partialLen, int expected)
    {
        var partial = new byte[partialLen];
        if (partialLen > 0)
            Buffer.BlockCopy(_beBlob, 0, partial, 0, Math.Min(partialLen, _beBlob.Length));
        Assert.Equal(expected, _marshaller.BytesNeeded(partial, partialLen));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(32)]
    public void BytesNeeded_BigEndianSufficientData_ReturnsFullLength(int partialLen)
    {
        var partial = new byte[partialLen];
        Buffer.BlockCopy(_beBlob, 0, partial, 0, Math.Min(partialLen, _beBlob.Length));
        Assert.Equal(_beBlob.Length, _marshaller.BytesNeeded(partial, partialLen));
    }

    [Fact]
    public void AppendBodyAndRemarshal_LittleEndianMessage_UpdatesLengthAndPreservesHeader()
    {
        VerifyAppendAndRemarshal(_leBlob, EndianFlag.Little);
    }

    [Fact]
    public void AppendBodyAndRemarshal_BigEndianMessage_UpdatesLengthAndPreservesHeader()
    {
        VerifyAppendAndRemarshal(_beBlob, EndianFlag.Big);
    }

    private void VerifyAppendAndRemarshal(byte[] blob, EndianFlag endianness)
    {
        var msg = _marshaller.Demarshal(blob);
        var builder = (NDeskMessageBuilder)msg;

        // Append second UINT32
        var writer = new MessageWriter(endianness);
        writer.Write((uint)0x55667788);
        var newBody = writer.ToArray();

        var existingBody = builder.Message.Body ?? [];
        var combined = new byte[existingBody.Length + newBody.Length];
        Buffer.BlockCopy(existingBody, 0, combined, 0, existingBody.Length);
        Buffer.BlockCopy(newBody, 0, combined, existingBody.Length, newBody.Length);
        builder.Message.Body = combined;
        msg.SetSignature("uu");

        var output = _marshaller.Marshal(msg);

        // Endian marker
        Assert.True(output[0] == (byte)'l' || output[0] == (byte)'B');

        // Type, flags, version match original
        Assert.Equal(blob[1], output[1]); // message type
        Assert.Equal(blob[2], output[2]); // flags
        Assert.Equal(blob[3], output[3]); // version

        // Body length = 8 (two UINT32s)
        uint bodyLen;
        if (output[0] == (byte)'l')
            bodyLen = (uint)(output[4] | (output[5] << 8) | (output[6] << 16) | (output[7] << 24));
        else
            bodyLen = (uint)((output[4] << 24) | (output[5] << 16) | (output[6] << 8) | output[7]);
        Assert.Equal(8u, bodyLen);

        // Serial matches TestSerial
        uint serial;
        if (output[0] == (byte)'l')
            serial = (uint)(output[8] | (output[9] << 8) | (output[10] << 16) | (output[11] << 24));
        else
            serial = (uint)((output[8] << 24) | (output[9] << 16) | (output[10] << 8) | output[11]);
        Assert.Equal(TestSerial, serial);

        // Total length = original length + 4 (one additional uint)
        Assert.Equal(blob.Length + 4, output.Length);
    }
}
