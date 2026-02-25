using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class ScenarioMatrixTests
{
    private readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();

    public static IEnumerable<object[]> ByteOrders()
    {
        yield return [ByteOrder.LittleEndian];
        yield return [ByteOrder.BigEndian];
    }

    public static IEnumerable<object[]> OffsetAndByteOrders()
    {
        foreach (var offset in Enumerable.Range(0, 10))
        foreach (var order in new[] { ByteOrder.LittleEndian, ByteOrder.BigEndian })
            yield return [offset, order];
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadInt32Array_ZeroElements_RoundTripsEmptyArray(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray(Array.Empty<int>());
        var bytes = writer.ToArray();
        Assert.NotEmpty(bytes); // At minimum the length prefix

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<int>();
        Assert.Empty(result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadInt32Array_OneElement_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray([42]);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<int>();
        Assert.Single(result);
        Assert.Equal(42, result[0]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadInt32Array_TwoElements_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray([100, 200]);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<int>();
        Assert.Equal(2, result.Length);
        Assert.Equal(100, result[0]);
        Assert.Equal(200, result[1]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadInt32Array_NineElements_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        var input = Enumerable.Range(1, 9).ToArray();
        writer.WriteArray(input);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<int>();
        Assert.Equal(9, result.Length);
        Assert.Equal(input, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadStringArray_ZeroElements_RoundTripsEmptyArray(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray(Array.Empty<string>());
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<string>();
        Assert.Empty(result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadStringArray_TwoElements_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray(["hello", "world"]);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<string>();
        Assert.Equal(2, result.Length);
        Assert.Equal("hello", result[0]);
        Assert.Equal("world", result[1]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadByteArray_OneElement_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray(new byte[] { 0xFF });
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<byte>();
        Assert.Single(result);
        Assert.Equal(0xFF, result[0]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadByteArray_NineElements_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        var input = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        writer.WriteArray(input);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<byte>();
        Assert.Equal(9, result.Length);
        Assert.Equal(input, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadDoubleArray_TwoElements_RoundTripsValues(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteArray([1.5, 3.14]);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadArray<double>();
        Assert.Equal(2, result.Length);
        Assert.Equal(1.5, result[0]);
        Assert.Equal(3.14, result[1]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadVariant_Int32Value_RoundTripsValue(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteVariant((int)42);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadVariant();
        Assert.Equal(42, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadVariant_StringValue_RoundTripsValue(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteVariant("hello");
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadVariant();
        Assert.Equal("hello", result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadVariant_BooleanValue_RoundTripsValue(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteVariant(true);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadVariant();
        Assert.Equal(true, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadVariant_ByteValue_RoundTripsValue(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteVariant((byte)255);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadVariant();
        Assert.Equal((byte)255, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadVariant_DoubleValue_RoundTripsValue(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteVariant(3.14);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadVariant();
        Assert.Equal(3.14, result);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadDictionaryInt32String_SingleEntry_RoundTripsEntry(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteDictionary(new Dictionary<int, string> { { 42, "hello" } });
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadDictionary<int, string>();
        Assert.Single(result);
        Assert.Equal("hello", result[42]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadDictionaryStringInt32_TwoEntries_RoundTripsEntries(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteDictionary(new Dictionary<string, int>
        {
            { "alpha", 1 },
            { "beta", 2 }
        });
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadDictionary<string, int>();
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result["alpha"]);
        Assert.Equal(2, result["beta"]);
    }

    [Theory]
    [MemberData(nameof(ByteOrders))]
    public void WriteReadDictionaryByteBoolean_SingleEntry_RoundTripsEntry(ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);
        writer.WriteDictionary(new Dictionary<byte, bool> { { 0x01, true } });
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        var result = reader.ReadDictionary<byte, bool>();
        Assert.Single(result);
        Assert.True(result[0x01]);
    }

    // For each basic type, test write/read at various buffer offsets (0-9)
    // in both byte orders. This exercises alignment.
    
    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadByte_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteByte(0xAB);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadByte();
        Assert.Equal(0xAB, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadBoolean_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteBoolean(true);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadBoolean();
        Assert.True(result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadInt16_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteInt16(12345);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadInt16();
        Assert.Equal(12345, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadUInt16_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteUInt16(54321);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadUInt16();
        Assert.Equal((ushort)54321, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadInt32_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteInt32(42);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadInt32();
        Assert.Equal(42, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadUInt32_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteUInt32(0xDEADBEEF);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadUInt32();
        Assert.Equal(0xDEADBEEF, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadInt64_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteInt64(long.MaxValue);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadInt64();
        Assert.Equal(long.MaxValue, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadUInt64_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteUInt64(0xFEDCBA9876543210);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadUInt64();
        Assert.Equal(0xFEDCBA9876543210, result);
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadDouble_WithOffsetPadding_RoundTripsBitPattern(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteDouble(3.14159265);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadDouble();
        Assert.Equal(BitConverter.DoubleToInt64Bits(3.14159265), BitConverter.DoubleToInt64Bits(result));
    }

    [Theory]
    [MemberData(nameof(OffsetAndByteOrders))]
    public void WriteReadString_WithOffsetPadding_RoundTripsValue(int offset, ByteOrder order)
    {
        using var writer = _factory.CreateWriter(order);

        for (var i = 0; i < offset; i++)
            writer.WriteByte(0);

        writer.WriteString("test");
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(order, bytes);
        for (var i = 0; i < offset; i++)
            reader.ReadByte();

        var result = reader.ReadString();
        Assert.Equal("test", result);
    }
}
