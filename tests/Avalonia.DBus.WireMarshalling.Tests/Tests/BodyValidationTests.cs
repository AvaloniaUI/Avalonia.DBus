using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class BodyValidationTests
{
    private readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public void WriteReadBodySequence_MixedTypes_ReturnsExpectedValues(ByteOrder order)
    {
        // Write various types
        using var writer = _factory.CreateWriter(order);
        writer.WriteInt32(42);
        writer.WriteString("hello");
        writer.WriteBoolean(true);
        writer.WriteDouble(3.14);
        var bytes = writer.ToArray();

        // Read them back
        var reader = _factory.CreateReader(order, bytes);
        Assert.Equal(42, reader.ReadInt32());
        Assert.Equal("hello", reader.ReadString());
        Assert.True(reader.ReadBoolean());
        Assert.Equal(BitConverter.DoubleToInt64Bits(3.14),
                     BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public void WriteReadBodySequence_ScalarTypes_ReturnsExpectedValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteByte(0xAB);
        writer.WriteInt16(-1234);
        writer.WriteUInt16(5678);
        writer.WriteUInt32(0xDEADBEEF);
        writer.WriteInt64(-999999999999L);
        writer.WriteUInt64(0x123456789ABCDEF0UL);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        Assert.Equal(0xAB, reader.ReadByte());
        Assert.Equal((short)-1234, reader.ReadInt16());
        Assert.Equal((ushort)5678, reader.ReadUInt16());
        Assert.Equal(0xDEADBEEFu, reader.ReadUInt32());
        Assert.Equal(-999999999999L, reader.ReadInt64());
        Assert.Equal(0x123456789ABCDEF0UL, reader.ReadUInt64());
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public void WriteReadBodySequence_StringsAndBooleans_ReturnsExpectedValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteString("first");
        writer.WriteBoolean(false);
        writer.WriteString("");
        writer.WriteBoolean(true);
        writer.WriteString("last");
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        Assert.Equal("first", reader.ReadString());
        Assert.False(reader.ReadBoolean());
        Assert.Equal("", reader.ReadString());
        Assert.True(reader.ReadBoolean());
        Assert.Equal("last", reader.ReadString());
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public void WriteReadBodySequence_Arrays_ReturnsExpectedValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray(new uint[] { 1, 2, 3 });
        writer.WriteArray(["a", "b"]);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        Assert.Equal(new uint[] { 1, 2, 3 }, reader.ReadArray<uint>());
        Assert.Equal(new[] { "a", "b" }, reader.ReadArray<string>());
    }

    [Theory]
    [InlineData(ByteOrder.LittleEndian)]
    [InlineData(ByteOrder.BigEndian)]
    public void WriteReadBodySequence_ScalarsAndArrays_ReturnsExpectedValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteInt32(100);
        writer.WriteArray(new byte[] { 0x01, 0x02, 0x03 });
        writer.WriteDouble(2.718281828);
        writer.WriteString("end");
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        Assert.Equal(100, reader.ReadInt32());
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, reader.ReadArray<byte>());
        Assert.Equal(BitConverter.DoubleToInt64Bits(2.718281828),
                     BitConverter.DoubleToInt64Bits(reader.ReadDouble()));
        Assert.Equal("end", reader.ReadString());
    }
}
