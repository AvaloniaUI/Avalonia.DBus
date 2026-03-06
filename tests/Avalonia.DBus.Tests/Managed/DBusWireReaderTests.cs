using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using Avalonia.DBus.Managed;
using Xunit;

namespace Avalonia.DBus.Tests.Managed;

public class DBusWireReaderTests
{
    // ========================================================================
    // Helper: round-trip through writer -> reader
    // ========================================================================

    private static byte[] WriteWith(Action<DBusWireWriter> action)
    {
        var writer = new DBusWireWriter();
        action(writer);
        return writer.ToArray();
    }

    // --- ReadByte ---

    [Theory]
    [InlineData((byte)0x00)]
    [InlineData((byte)0x42)]
    [InlineData((byte)0xFF)]
    [InlineData((byte)0x7F)]
    public void ReadByte_RoundTrips(byte value)
    {
        var data = WriteWith(w => w.WriteByte(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadByte());
    }

    [Fact]
    public void ReadByte_AdvancesPositionByOne()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteByte(0x02);
        });
        var reader = new DBusWireReader(data);
        Assert.Equal(0, reader.Position);
        reader.ReadByte();
        Assert.Equal(1, reader.Position);
        reader.ReadByte();
        Assert.Equal(2, reader.Position);
    }

    // --- ReadBoolean ---

    [Fact]
    public void ReadBoolean_True_RoundTrips()
    {
        var data = WriteWith(w => w.WriteBoolean(true));
        var reader = new DBusWireReader(data);
        Assert.True(reader.ReadBoolean());
    }

    [Fact]
    public void ReadBoolean_False_RoundTrips()
    {
        var data = WriteWith(w => w.WriteBoolean(false));
        var reader = new DBusWireReader(data);
        Assert.False(reader.ReadBoolean());
    }

    [Fact]
    public void ReadBoolean_NonZeroNonOne_ReturnsTrue()
    {
        // D-Bus spec: boolean is uint32, any non-zero is true
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 42);
        var reader = new DBusWireReader(data);
        Assert.True(reader.ReadBoolean());
    }

    [Fact]
    public void ReadBoolean_PadsTo4ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0xFF);
            w.WriteBoolean(true);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        Assert.True(reader.ReadBoolean());
        Assert.Equal(8, reader.Position);
    }

    // --- ReadInt16 ---

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)-1)]
    [InlineData(short.MinValue)]
    [InlineData(short.MaxValue)]
    public void ReadInt16_RoundTrips(short value)
    {
        var data = WriteWith(w => w.WriteInt16(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadInt16());
    }

    [Fact]
    public void ReadInt16_PadsTo2ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0xAA);
            w.WriteInt16(0x1234);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        Assert.Equal(0x1234, reader.ReadInt16());
        Assert.Equal(4, reader.Position);
    }

    // --- ReadUInt16 ---

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    [InlineData(ushort.MaxValue)]
    public void ReadUInt16_RoundTrips(ushort value)
    {
        var data = WriteWith(w => w.WriteUInt16(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadUInt16());
    }

    // --- ReadInt32 ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void ReadInt32_RoundTrips(int value)
    {
        var data = WriteWith(w => w.WriteInt32(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadInt32());
    }

    [Fact]
    public void ReadInt32_PadsTo4ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteByte(0x02);
            w.WriteInt32(42);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        reader.ReadByte();
        Assert.Equal(42, reader.ReadInt32());
        Assert.Equal(8, reader.Position);
    }

    // --- ReadUInt32 ---

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(uint.MaxValue)]
    public void ReadUInt32_RoundTrips(uint value)
    {
        var data = WriteWith(w => w.WriteUInt32(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadUInt32());
    }

    // --- ReadInt64 ---

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void ReadInt64_RoundTrips(long value)
    {
        var data = WriteWith(w => w.WriteInt64(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadInt64());
    }

    [Fact]
    public void ReadInt64_PadsTo8ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteInt64(123456789L);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        Assert.Equal(123456789L, reader.ReadInt64());
        Assert.Equal(16, reader.Position);
    }

    // --- ReadUInt64 ---

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(ulong.MaxValue)]
    public void ReadUInt64_RoundTrips(ulong value)
    {
        var data = WriteWith(w => w.WriteUInt64(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadUInt64());
    }

    // --- ReadDouble ---

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(3.14159265358979)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void ReadDouble_RoundTrips(double value)
    {
        var data = WriteWith(w => w.WriteDouble(value));
        var reader = new DBusWireReader(data);
        Assert.Equal(value, reader.ReadDouble());
    }

    [Fact]
    public void ReadDouble_PadsTo8ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteDouble(1.0);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        Assert.Equal(1.0, reader.ReadDouble());
        Assert.Equal(16, reader.Position);
    }

    // --- ReadString ---

    [Fact]
    public void ReadString_EmptyString_RoundTrips()
    {
        var data = WriteWith(w => w.WriteString(""));
        var reader = new DBusWireReader(data);
        Assert.Equal("", reader.ReadString());
    }

    [Fact]
    public void ReadString_AsciiString_RoundTrips()
    {
        var data = WriteWith(w => w.WriteString("Hello"));
        var reader = new DBusWireReader(data);
        Assert.Equal("Hello", reader.ReadString());
    }

    [Fact]
    public void ReadString_Utf8MultiByte_RoundTrips()
    {
        var data = WriteWith(w => w.WriteString("\u00e9")); // e-acute
        var reader = new DBusWireReader(data);
        Assert.Equal("\u00e9", reader.ReadString());
    }

    [Fact]
    public void ReadString_Utf8ThreeByte_RoundTrips()
    {
        // U+2603 SNOWMAN = 3 UTF-8 bytes
        var data = WriteWith(w => w.WriteString("\u2603"));
        var reader = new DBusWireReader(data);
        Assert.Equal("\u2603", reader.ReadString());
    }

    [Fact]
    public void ReadString_Utf8FourByte_RoundTrips()
    {
        // U+1F600 GRINNING FACE = 4 UTF-8 bytes (surrogate pair in C#)
        string emoji = "\U0001F600";
        var data = WriteWith(w => w.WriteString(emoji));
        var reader = new DBusWireReader(data);
        Assert.Equal(emoji, reader.ReadString());
    }

    [Fact]
    public void ReadString_PadsLengthPrefixTo4ByteAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteString("AB");
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        Assert.Equal("AB", reader.ReadString());
    }

    [Fact]
    public void ReadString_TruncatedPayload_ThrowsInvalidDataException()
    {
        // length = 5, but only 3 bytes + null terminator present
        var data = new byte[4 + 3 + 1];
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0, 4), 5);
        Encoding.UTF8.GetBytes("abc").CopyTo(data, 4);
        data[7] = 0;

        var reader = new DBusWireReader(data);
        Assert.Throws<InvalidDataException>(() => reader.ReadString());
    }

    // --- ReadObjectPath ---

    [Fact]
    public void ReadObjectPath_RoundTrips()
    {
        var data = WriteWith(w => w.WriteObjectPath("/org/freedesktop"));
        var reader = new DBusWireReader(data);
        Assert.Equal("/org/freedesktop", reader.ReadObjectPath());
    }

    [Fact]
    public void ReadObjectPath_SameFormatAsString()
    {
        var data = WriteWith(w => w.WriteObjectPath("/org/freedesktop"));
        var readerPath = new DBusWireReader(data);
        var readerString = new DBusWireReader(data);
        Assert.Equal(readerString.ReadString(), readerPath.ReadObjectPath());
    }

    // --- ReadSignature ---

    [Fact]
    public void ReadSignature_EmptySignature_RoundTrips()
    {
        var data = WriteWith(w => w.WriteSignature(""));
        var reader = new DBusWireReader(data);
        Assert.Equal("", reader.ReadSignature());
    }

    [Fact]
    public void ReadSignature_SimpleSignature_RoundTrips()
    {
        var data = WriteWith(w => w.WriteSignature("su"));
        var reader = new DBusWireReader(data);
        Assert.Equal("su", reader.ReadSignature());
    }

    [Fact]
    public void ReadSignature_HasNoPadding()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0xFF);
            w.WriteSignature("i");
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        // Signature has no alignment padding, starts immediately
        Assert.Equal("i", reader.ReadSignature());
        Assert.Equal(4, reader.Position); // 1 (byte) + 1 (len) + 1 (i) + 1 (null) = 4
    }

    [Fact]
    public void ReadSignature_LongSignature_RoundTrips()
    {
        // Maximum useful: complex struct signature
        string sig = "a{sv}(iiusb)";
        var data = WriteWith(w => w.WriteSignature(sig));
        var reader = new DBusWireReader(data);
        Assert.Equal(sig, reader.ReadSignature());
    }

    // --- ReadPad ---

    [Fact]
    public void ReadPad_AlreadyAligned_DoesNotAdvance()
    {
        var data = new byte[8];
        var reader = new DBusWireReader(data);
        reader.ReadPad(4); // position 0 is already 4-aligned
        Assert.Equal(0, reader.Position);
    }

    [Fact]
    public void ReadPad_AdvancesToAlignment()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WritePad(4);
        });
        var reader = new DBusWireReader(data);
        reader.ReadByte();
        reader.ReadPad(4);
        Assert.Equal(4, reader.Position);
    }

    [Theory]
    [InlineData(1, 8, 8)]
    [InlineData(2, 4, 4)]
    [InlineData(3, 2, 4)]
    [InlineData(4, 4, 4)]
    [InlineData(5, 8, 8)]
    public void ReadPad_AdvancesToCorrectPosition(int bytesRead, int alignment, int expectedPosition)
    {
        // Create data large enough
        var data = new byte[16];
        var reader = new DBusWireReader(data);
        for (int i = 0; i < bytesRead; i++)
            reader.ReadByte();
        reader.ReadPad(alignment);
        Assert.Equal(expectedPosition, reader.Position);
    }

    [Fact]
    public void ReadPad_ZeroAlignment_ThrowsArgumentOutOfRangeException()
    {
        var reader = new DBusWireReader(new byte[8]);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.ReadPad(0));
    }

    // --- ReadNull ---

    [Fact]
    public void ReadNull_ReadsZeroByte()
    {
        var data = new byte[] { 0 };
        var reader = new DBusWireReader(data);
        reader.ReadNull(); // should not throw
        Assert.Equal(1, reader.Position);
    }

    // --- Position get/set ---

    [Fact]
    public void Position_TracksCurrentReadOffset()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x01);
            w.WriteInt32(42);
        });
        var reader = new DBusWireReader(data);
        Assert.Equal(0, reader.Position);
        reader.ReadByte();
        Assert.Equal(1, reader.Position);
        reader.ReadInt32();
        Assert.Equal(8, reader.Position);
    }

    [Fact]
    public void Position_CanBeSet()
    {
        var data = WriteWith(w =>
        {
            w.WriteInt32(42);
            w.WriteInt32(99);
        });
        var reader = new DBusWireReader(data);
        reader.ReadInt32(); // read 42
        reader.Position = 0; // seek back
        Assert.Equal(42, reader.ReadInt32()); // re-read
    }

    [Fact]
    public void Position_SetNegative_ThrowsArgumentOutOfRangeException()
    {
        var reader = new DBusWireReader(new byte[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = -1);
    }

    [Fact]
    public void Position_SetPastBufferEnd_ThrowsArgumentOutOfRangeException()
    {
        var reader = new DBusWireReader(new byte[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.Position = 5);
    }

    [Fact]
    public void ReadByte_AtEndOfBuffer_ThrowsInvalidDataException()
    {
        var reader = new DBusWireReader(new byte[] { 0x01 });
        Assert.Equal(0x01, reader.ReadByte());
        Assert.Throws<InvalidDataException>(() => reader.ReadByte());
    }

    // ========================================================================
    // Combined / multi-value round-trip tests
    // ========================================================================

    [Fact]
    public void MultiplePrimitives_RoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0xAA);
            w.WriteInt32(0x1234);
            w.WriteByte(0xBB);
            w.WriteInt64(0x5678);
        });

        var reader = new DBusWireReader(data);
        Assert.Equal(0xAA, reader.ReadByte());
        Assert.Equal(0x1234, reader.ReadInt32());
        Assert.Equal(0xBB, reader.ReadByte());
        Assert.Equal(0x5678L, reader.ReadInt64());
    }

    [Fact]
    public void StringThenSignature_RoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteString("Hi");
            w.WriteSignature("s");
        });

        var reader = new DBusWireReader(data);
        Assert.Equal("Hi", reader.ReadString());
        Assert.Equal("s", reader.ReadSignature());
    }

    [Fact]
    public void AllTypes_SequentialRoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(0x42);
            w.WriteBoolean(true);
            w.WriteInt16(-100);
            w.WriteUInt16(65535);
            w.WriteInt32(-2_000_000);
            w.WriteUInt32(4_000_000_000);
            w.WriteInt64(long.MinValue);
            w.WriteUInt64(ulong.MaxValue);
            w.WriteDouble(2.71828);
            w.WriteString("hello world");
            w.WriteObjectPath("/org/example");
            w.WriteSignature("a{sv}");
        });

        var reader = new DBusWireReader(data);
        Assert.Equal(0x42, reader.ReadByte());
        Assert.True(reader.ReadBoolean());
        Assert.Equal((short)-100, reader.ReadInt16());
        Assert.Equal((ushort)65535, reader.ReadUInt16());
        Assert.Equal(-2_000_000, reader.ReadInt32());
        Assert.Equal(4_000_000_000u, reader.ReadUInt32());
        Assert.Equal(long.MinValue, reader.ReadInt64());
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
        Assert.Equal(2.71828, reader.ReadDouble());
        Assert.Equal("hello world", reader.ReadString());
        Assert.Equal("/org/example", reader.ReadObjectPath());
        Assert.Equal("a{sv}", reader.ReadSignature());
    }

    [Fact]
    public void MultipleStrings_RoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteString("first");
            w.WriteString("second");
            w.WriteString("");
            w.WriteString("fourth");
        });

        var reader = new DBusWireReader(data);
        Assert.Equal("first", reader.ReadString());
        Assert.Equal("second", reader.ReadString());
        Assert.Equal("", reader.ReadString());
        Assert.Equal("fourth", reader.ReadString());
    }

    [Fact]
    public void BoundaryValues_AllIntegerTypes_RoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteByte(byte.MinValue);
            w.WriteByte(byte.MaxValue);
            w.WriteInt16(short.MinValue);
            w.WriteInt16(short.MaxValue);
            w.WriteUInt16(ushort.MinValue);
            w.WriteUInt16(ushort.MaxValue);
            w.WriteInt32(int.MinValue);
            w.WriteInt32(int.MaxValue);
            w.WriteUInt32(uint.MinValue);
            w.WriteUInt32(uint.MaxValue);
            w.WriteInt64(long.MinValue);
            w.WriteInt64(long.MaxValue);
            w.WriteUInt64(ulong.MinValue);
            w.WriteUInt64(ulong.MaxValue);
        });

        var reader = new DBusWireReader(data);
        Assert.Equal(byte.MinValue, reader.ReadByte());
        Assert.Equal(byte.MaxValue, reader.ReadByte());
        Assert.Equal(short.MinValue, reader.ReadInt16());
        Assert.Equal(short.MaxValue, reader.ReadInt16());
        Assert.Equal(ushort.MinValue, reader.ReadUInt16());
        Assert.Equal(ushort.MaxValue, reader.ReadUInt16());
        Assert.Equal(int.MinValue, reader.ReadInt32());
        Assert.Equal(int.MaxValue, reader.ReadInt32());
        Assert.Equal(uint.MinValue, reader.ReadUInt32());
        Assert.Equal(uint.MaxValue, reader.ReadUInt32());
        Assert.Equal(long.MinValue, reader.ReadInt64());
        Assert.Equal(long.MaxValue, reader.ReadInt64());
        Assert.Equal(ulong.MinValue, reader.ReadUInt64());
        Assert.Equal(ulong.MaxValue, reader.ReadUInt64());
    }

    [Fact]
    public void BoundaryValues_Double_SpecialValues_RoundTrip()
    {
        var data = WriteWith(w =>
        {
            w.WriteDouble(double.Epsilon);
            w.WriteDouble(double.NegativeInfinity);
            w.WriteDouble(double.PositiveInfinity);
            w.WriteDouble(double.NaN);
        });

        var reader = new DBusWireReader(data);
        Assert.Equal(double.Epsilon, reader.ReadDouble());
        Assert.Equal(double.NegativeInfinity, reader.ReadDouble());
        Assert.Equal(double.PositiveInfinity, reader.ReadDouble());
        Assert.True(double.IsNaN(reader.ReadDouble()));
    }

    // ========================================================================
    // Big-endian reader tests
    // ========================================================================

    private static byte[] WriteBigEndian(Action<byte[], int> write, int size)
    {
        var data = new byte[size];
        write(data, 0);
        return data;
    }

    [Fact]
    public void BigEndian_ReadInt16()
    {
        var data = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(data, 0x1234);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0x1234, reader.ReadInt16());
    }

    [Fact]
    public void BigEndian_ReadUInt16()
    {
        var data = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(data, 0xABCD);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0xABCD, reader.ReadUInt16());
    }

    [Fact]
    public void BigEndian_ReadInt32()
    {
        var data = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(data, 0x12345678);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0x12345678, reader.ReadInt32());
    }

    [Fact]
    public void BigEndian_ReadUInt32()
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, 0xDEADBEEF);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0xDEADBEEF, reader.ReadUInt32());
    }

    [Fact]
    public void BigEndian_ReadInt64()
    {
        var data = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(data, 0x123456789ABCDEF0);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0x123456789ABCDEF0, reader.ReadInt64());
    }

    [Fact]
    public void BigEndian_ReadUInt64()
    {
        var data = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(data, 0xFEDCBA9876543210);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0xFEDCBA9876543210, reader.ReadUInt64());
    }

    [Fact]
    public void BigEndian_ReadBoolean()
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(data, 1);
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.True(reader.ReadBoolean());
    }

    [Fact]
    public void BigEndian_ReadDouble()
    {
        var data = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(data, BitConverter.DoubleToInt64Bits(3.14));
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(3.14, reader.ReadDouble());
    }

    [Fact]
    public void BigEndian_ReadString()
    {
        // String: uint32 length (BE) + UTF-8 bytes + null terminator
        var data = new byte[4 + 5 + 1]; // "Hello" = 5 bytes
        BinaryPrimitives.WriteUInt32BigEndian(data, 5);
        Encoding.UTF8.GetBytes("Hello").CopyTo(data, 4);
        data[9] = 0; // null terminator
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal("Hello", reader.ReadString());
    }

    [Fact]
    public void BigEndian_ReadString_WouldMisreadAsLittleEndian()
    {
        // Verify that a BE-encoded string length is correctly interpreted.
        // Length 5 in BE = [0x00, 0x00, 0x00, 0x05]
        // If misread as LE, length would be 0x05000000 (83886080) — way too large.
        var data = new byte[4 + 5 + 1];
        BinaryPrimitives.WriteUInt32BigEndian(data, 5);
        Encoding.UTF8.GetBytes("Hello").CopyTo(data, 4);
        data[9] = 0;
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal("Hello", reader.ReadString());
    }

    [Fact]
    public void BigEndian_AllTypes_SequentialRead()
    {
        // Build a BE buffer manually: byte, boolean(uint32), int16, uint16, int32, uint32
        using var ms = new System.IO.MemoryStream();
        // byte (pos 0)
        ms.WriteByte(0x42);
        // boolean: pad to 4, write uint32=1 (pos 1 -> pad to 4, then 4 bytes)
        ms.WriteByte(0); ms.WriteByte(0); ms.WriteByte(0); // pad to 4
        var buf4 = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buf4, 1);
        ms.Write(buf4); // pos 4-7
        // int16: pad to 2 (pos 8, already aligned)
        var buf2 = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(buf2, -100);
        ms.Write(buf2); // pos 8-9
        // uint16: pad to 2 (pos 10, already aligned)
        BinaryPrimitives.WriteUInt16BigEndian(buf2, 65535);
        ms.Write(buf2); // pos 10-11
        // int32: pad to 4 (pos 12, already aligned)
        BinaryPrimitives.WriteInt32BigEndian(buf4, -2_000_000);
        ms.Write(buf4); // pos 12-15
        // uint32: pad to 4 (pos 16, already aligned)
        BinaryPrimitives.WriteUInt32BigEndian(buf4, 4_000_000_000);
        ms.Write(buf4); // pos 16-19

        var data = ms.ToArray();
        var reader = new DBusWireReader(data, bigEndian: true);
        Assert.Equal(0x42, reader.ReadByte());
        Assert.True(reader.ReadBoolean());
        Assert.Equal((short)-100, reader.ReadInt16());
        Assert.Equal((ushort)65535, reader.ReadUInt16());
        Assert.Equal(-2_000_000, reader.ReadInt32());
        Assert.Equal(4_000_000_000u, reader.ReadUInt32());
    }

    [Fact]
    public void DefaultConstructor_IsLittleEndian()
    {
        // Verify the default (no bigEndian param) still reads LE correctly
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 42);
        var reader = new DBusWireReader(data);
        Assert.Equal(42u, reader.ReadUInt32());
    }
}
