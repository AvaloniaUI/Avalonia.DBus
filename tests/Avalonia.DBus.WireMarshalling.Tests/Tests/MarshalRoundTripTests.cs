using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class MarshalRoundTripTests
{
    private readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();

    public static IEnumerable<object[]> Int16WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            _ => new short[] { -1, 0, 1, 0x7FFF, short.MinValue }, (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(Int16WriteReadData))]
    public void WriteReadInt16_BothByteOrders_RoundTripsValue(ByteOrder order, short value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteInt16(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadInt16();
        Assert.Equal(value, result);
    }


    public static IEnumerable<object[]> UInt16WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(order => new ushort[] { 0, 1, 0x7FFF, 0xFFFF },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(UInt16WriteReadData))]
    public void WriteReadUInt16_BothByteOrders_RoundTripsValue(ByteOrder order, ushort value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteUInt16(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadUInt16();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> Int32WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            order => new[] { -1, 0, 1, 0x7FFFFFFF, int.MinValue, -12345 }, (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(Int32WriteReadData))]
    public void WriteReadInt32_BothByteOrders_RoundTripsValue(ByteOrder order, int value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteInt32(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadInt32();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> UInt32WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            order => new uint[] { 0, 1, 0x7FFFFFFF, 0xFFFFFFFF, 0x12345678 },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(UInt32WriteReadData))]
    public void WriteReadUInt32_BothByteOrders_RoundTripsValue(ByteOrder order, uint value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteUInt32(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadUInt32();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> Int64WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            order => new[] { -1L, 0L, 1L, long.MaxValue, long.MinValue }, (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(Int64WriteReadData))]
    public void WriteReadInt64_BothByteOrders_RoundTripsValue(ByteOrder order, long value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteInt64(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadInt64();
        Assert.Equal(value, result);
    }


    public static IEnumerable<object[]> UInt64WriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            _ => new[] { 0UL, 1UL, ulong.MaxValue, 0x123456789ABCDEF0UL },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(UInt64WriteReadData))]
    public void WriteReadUInt64_BothByteOrders_RoundTripsValue(ByteOrder order, ulong value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteUInt64(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadUInt64();
        Assert.Equal(value, result);
    }


    public static IEnumerable<object[]> DoubleWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            _ => new[] { 0.0, 1.5, -1.5, 3.14159265, double.MaxValue, double.MinValue },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(DoubleWriteReadData))]
    public void WriteReadDouble_BothByteOrders_RoundTripsBitPattern(ByteOrder order, double value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteDouble(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadDouble();
        Assert.Equal(BitConverter.DoubleToInt64Bits(value), BitConverter.DoubleToInt64Bits(result));
    }

    public static IEnumerable<object[]> StringWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            _ => new[] { "", "hello", "hello world", "non-ascii: \u00e4\u00f6\u00fc", "a", "abc", "abcdefgh" },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(StringWriteReadData))]
    public void WriteReadString_BothByteOrders_RoundTripsValue(ByteOrder order, string value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteString(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadString();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> ObjectPathWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            order => new[] { "/", "/foo", "/foo/bar", "/foo/bar/baz/qux/a/b/c/d" },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(ObjectPathWriteReadData))]
    public void WriteReadObjectPath_BothByteOrders_RoundTripsValue(ByteOrder order, string value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteObjectPath(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadObjectPath();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> SignatureWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(
            order => new[] { "", "i", "ai", "(ii)", "a{sv}", "a(iis)" }, (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(SignatureWriteReadData))]
    public void WriteReadSignature_BothByteOrders_RoundTripsValue(ByteOrder order, string value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteSignature(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadSignature();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> BooleanWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(order => new[] { true, false },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(BooleanWriteReadData))]
    public void WriteReadBoolean_BothByteOrders_RoundTripsValue(ByteOrder order, bool value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteBoolean(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadBoolean();
        Assert.Equal(value, result);
    }

    public static IEnumerable<object[]> ByteWriteReadData() =>
        new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian }.SelectMany(order => new byte[] { 0, 1, 127, 255 },
            (order, value) => (object[])[order, value]);

    [Theory]
    [MemberData(nameof(ByteWriteReadData))]
    public void WriteReadByte_BothByteOrders_RoundTripsValue(ByteOrder order, byte value)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteByte(value);
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes);

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadByte();
        Assert.Equal(value, result);
    }
}
