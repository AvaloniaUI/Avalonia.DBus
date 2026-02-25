using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using Avalonia.DBus.WireMarshalling.Tests.NDesk;
using Xunit;

namespace Avalonia.DBus.WireMarshalling.Tests.Tests;

public class ByteOrderConversionTests
{
    private readonly IWireMarshallerFactory _factory = new NDeskMarshallerFactory();

    // Writing values in one order, reading back in the same order, should
    // always produce the original values regardless of byte order choice.
    [Fact]
    public void CompareValuesAcrossByteOrders_LittleToBig_LogicalValuesMatch()
    {
        VerifyByteOrderConversion(ByteOrder.LittleEndian, ByteOrder.BigEndian);
    }

    [Fact]
    public void CompareValuesAcrossByteOrders_BigToLittle_LogicalValuesMatch()
    {
        VerifyByteOrderConversion(ByteOrder.BigEndian, ByteOrder.LittleEndian);
    }

    private void VerifyByteOrderConversion(ByteOrder sourceOrder, ByteOrder targetOrder)
    {
        // For each type: write in source order, read back -> reference value.
        // Write same value in target order, read back -> comparison value.
        // They must match: same logical value despite different wire formats.

        // Byte (1 byte - unaffected by byte order)
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteByte(0xAB);
            var r1 = _factory.CreateReader(sourceOrder, w1.ToArray());
            var val1 = r1.ReadByte();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteByte(0xAB);
            var r2 = _factory.CreateReader(targetOrder, w2.ToArray());
            var val2 = r2.ReadByte();

            Assert.Equal(val1, val2);
        }

        // Boolean (encoded as uint32)
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteBoolean(true);
            var r1 = _factory.CreateReader(sourceOrder, w1.ToArray());
            var val1 = r1.ReadBoolean();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteBoolean(true);
            var r2 = _factory.CreateReader(targetOrder, w2.ToArray());
            var val2 = r2.ReadBoolean();

            Assert.Equal(val1, val2);
        }

        // Int16
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteInt16(0x1234);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadInt16();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteInt16(0x1234);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadInt16();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for Int16 in different byte orders");
        }

        // UInt16
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteUInt16(0xABCD);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadUInt16();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteUInt16(0xABCD);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadUInt16();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for UInt16 in different byte orders");
        }

        // Int32
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteInt32(0x12345678);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadInt32();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteInt32(0x12345678);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadInt32();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for Int32 in different byte orders");
        }

        // UInt32
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteUInt32(0xDEADBEEF);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadUInt32();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteUInt32(0xDEADBEEF);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadUInt32();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for UInt32 in different byte orders");
        }

        // Int64
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteInt64(0x123456789ABCDEF0);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadInt64();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteInt64(0x123456789ABCDEF0);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadInt64();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for Int64 in different byte orders");
        }

        // UInt64
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteUInt64(0xFEDCBA9876543210);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadUInt64();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteUInt64(0xFEDCBA9876543210);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadUInt64();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for UInt64 in different byte orders");
        }

        // Double
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteDouble(3.14159265);
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadDouble();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteDouble(3.14159265);
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadDouble();

            Assert.Equal(BitConverter.DoubleToInt64Bits(val1), BitConverter.DoubleToInt64Bits(val2));
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for Double in different byte orders");
        }

        // String (content is UTF-8 bytes, unaffected, but the uint32 length prefix differs)
        {
            using var w1 = _factory.CreateWriter(sourceOrder);
            w1.WriteString("hello");
            var bytes1 = w1.ToArray();
            var r1 = _factory.CreateReader(sourceOrder, bytes1);
            var val1 = r1.ReadString();

            using var w2 = _factory.CreateWriter(targetOrder);
            w2.WriteString("hello");
            var bytes2 = w2.ToArray();
            var r2 = _factory.CreateReader(targetOrder, bytes2);
            var val2 = r2.ReadString();

            Assert.Equal(val1, val2);
            if (sourceOrder != targetOrder)
                Assert.False(bytes1.SequenceEqual(bytes2), "Wire bytes should differ for String in different byte orders (length prefix)");
        }
    }

    // Writing in one byte order and reading with the other should produce
    // incorrect values for multi-byte types.
    [Fact]
    public void ReadWithWrongByteOrder_Int32Payload_ReturnsDifferentValue()
    {
        // Write in LE, attempt read in BE -> should get wrong value
        using var writer = _factory.CreateWriter(ByteOrder.LittleEndian);
        writer.WriteInt32(0x12345678);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(ByteOrder.BigEndian, bytes);
        var mismatchResult = reader.ReadInt32();
        Assert.NotEqual(0x12345678, mismatchResult);
    }

    [Fact]
    public void ReadWithWrongByteOrder_UInt32Payload_ReturnsDifferentValue()
    {
        using var writer = _factory.CreateWriter(ByteOrder.BigEndian);
        writer.WriteUInt32(0xDEADBEEF);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(ByteOrder.LittleEndian, bytes);
        var mismatchResult = reader.ReadUInt32();
        Assert.NotEqual(0xDEADBEEF, mismatchResult);
    }

    [Fact]
    public void ReadWithWrongByteOrder_Int16Payload_ReturnsDifferentValue()
    {
        using var writer = _factory.CreateWriter(ByteOrder.LittleEndian);
        writer.WriteInt16(0x1234);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(ByteOrder.BigEndian, bytes);
        var mismatchResult = reader.ReadInt16();
        Assert.NotEqual(0x1234, mismatchResult);
    }

    [Fact]
    public void ReadWithWrongByteOrder_Int64Payload_ReturnsDifferentValue()
    {
        using var writer = _factory.CreateWriter(ByteOrder.LittleEndian);
        writer.WriteInt64(0x123456789ABCDEF0);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(ByteOrder.BigEndian, bytes);
        var mismatchResult = reader.ReadInt64();
        Assert.NotEqual(0x123456789ABCDEF0, mismatchResult);
    }

    [Fact]
    public void ReadWithWrongByteOrder_DoublePayload_ReturnsDifferentValue()
    {
        using var writer = _factory.CreateWriter(ByteOrder.LittleEndian);
        writer.WriteDouble(3.14159265);
        var bytes = writer.ToArray();

        var reader = _factory.CreateReader(ByteOrder.BigEndian, bytes);
        var mismatchResult = reader.ReadDouble();
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(3.14159265), BitConverter.DoubleToInt64Bits(mismatchResult));
    }

    // For multi-byte types, verify that LE and BE wire bytes are reversed.

    [Fact]
    public void CompareWireBytes_Int16AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteInt16(0x0102);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteInt16(0x0102);
        var beBytes = beWriter.ToArray();

        Assert.Equal(2, leBytes.Length);
        Assert.Equal(2, beBytes.Length);
        Assert.Equal(leBytes[0], beBytes[1]);
        Assert.Equal(leBytes[1], beBytes[0]);
    }

    [Fact]
    public void CompareWireBytes_Int32AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteInt32(0x01020304);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteInt32(0x01020304);
        var beBytes = beWriter.ToArray();

        Assert.Equal(4, leBytes.Length);
        Assert.Equal(4, beBytes.Length);
        Assert.Equal(leBytes[0], beBytes[3]);
        Assert.Equal(leBytes[1], beBytes[2]);
        Assert.Equal(leBytes[2], beBytes[1]);
        Assert.Equal(leBytes[3], beBytes[0]);
    }

    [Fact]
    public void CompareWireBytes_Int64AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteInt64(0x0102030405060708);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteInt64(0x0102030405060708);
        var beBytes = beWriter.ToArray();

        Assert.Equal(8, leBytes.Length);
        Assert.Equal(8, beBytes.Length);
        for (var i = 0; i < 8; i++)
            Assert.Equal(leBytes[i], beBytes[7 - i]);
    }

    [Fact]
    public void CompareWireBytes_UInt16AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteUInt16(0x0102);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteUInt16(0x0102);
        var beBytes = beWriter.ToArray();

        Assert.Equal(2, leBytes.Length);
        Assert.Equal(2, beBytes.Length);
        Assert.Equal(leBytes[0], beBytes[1]);
        Assert.Equal(leBytes[1], beBytes[0]);
    }

    [Fact]
    public void CompareWireBytes_UInt32AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteUInt32(0x01020304);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteUInt32(0x01020304);
        var beBytes = beWriter.ToArray();

        Assert.Equal(4, leBytes.Length);
        Assert.Equal(4, beBytes.Length);
        Assert.Equal(leBytes[0], beBytes[3]);
        Assert.Equal(leBytes[1], beBytes[2]);
        Assert.Equal(leBytes[2], beBytes[1]);
        Assert.Equal(leBytes[3], beBytes[0]);
    }

    [Fact]
    public void CompareWireBytes_UInt64AcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteUInt64(0x0102030405060708);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteUInt64(0x0102030405060708);
        var beBytes = beWriter.ToArray();

        Assert.Equal(8, leBytes.Length);
        Assert.Equal(8, beBytes.Length);
        for (var i = 0; i < 8; i++)
            Assert.Equal(leBytes[i], beBytes[7 - i]);
    }

    [Fact]
    public void CompareWireBytes_DoubleAcrossEndianness_BytesAreReversed()
    {
        using var leWriter = _factory.CreateWriter(ByteOrder.LittleEndian);
        leWriter.WriteDouble(3.14159265);
        var leBytes = leWriter.ToArray();

        using var beWriter = _factory.CreateWriter(ByteOrder.BigEndian);
        beWriter.WriteDouble(3.14159265);
        var beBytes = beWriter.ToArray();

        Assert.Equal(8, leBytes.Length);
        Assert.Equal(8, beBytes.Length);
        for (var i = 0; i < 8; i++)
            Assert.Equal(leBytes[i], beBytes[7 - i]);
    }

}
