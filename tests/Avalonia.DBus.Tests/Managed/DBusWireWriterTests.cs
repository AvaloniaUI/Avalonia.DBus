using System;
using System.Buffers.Binary;
using System.Text;
using Avalonia.DBus.Managed;
using Xunit;

namespace Avalonia.DBus.Tests.Managed;

public class DBusWireWriterTests
{
    // --- WriteByte ---

    [Fact]
    public void WriteByte_WritesSingleByte()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x42);

        var bytes = writer.ToArray();
        Assert.Single(bytes);
        Assert.Equal(0x42, bytes[0]);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    [InlineData(0x7F)]
    public void WriteByte_VariousValues(byte value)
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(value);

        Assert.Equal(new[] { value }, writer.ToArray());
    }

    // --- WriteBoolean ---

    [Fact]
    public void WriteBoolean_True_WritesUInt32One()
    {
        var writer = new DBusWireWriter();
        writer.WriteBoolean(true);

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    [Fact]
    public void WriteBoolean_False_WritesUInt32Zero()
    {
        var writer = new DBusWireWriter();
        writer.WriteBoolean(false);

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    [Fact]
    public void WriteBoolean_PadsTo4ByteAlignment()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0xFF); // position 1
        writer.WriteBoolean(true); // should pad to position 4, then write 4 bytes

        var bytes = writer.ToArray();
        Assert.Equal(8, bytes.Length);
        // byte 0: 0xFF
        Assert.Equal(0xFF, bytes[0]);
        // bytes 1-3: padding zeros
        Assert.Equal(0, bytes[1]);
        Assert.Equal(0, bytes[2]);
        Assert.Equal(0, bytes[3]);
        // bytes 4-7: uint32 = 1 LE
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
    }

    // --- WriteInt16 / WriteUInt16 ---

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1)]
    [InlineData((short)-1)]
    [InlineData(short.MinValue)]
    [InlineData(short.MaxValue)]
    public void WriteInt16_WritesLittleEndian(short value)
    {
        var writer = new DBusWireWriter();
        writer.WriteInt16(value);

        var bytes = writer.ToArray();
        Assert.Equal(2, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadInt16LittleEndian(bytes));
    }

    [Fact]
    public void WriteInt16_PadsTo2ByteAlignment()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0xAA); // position 1
        writer.WriteInt16(0x1234); // should pad to position 2

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0xAA, bytes[0]);
        Assert.Equal(0, bytes[1]); // padding
        Assert.Equal(0x1234, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)));
    }

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)1)]
    [InlineData(ushort.MaxValue)]
    public void WriteUInt16_WritesLittleEndian(ushort value)
    {
        var writer = new DBusWireWriter();
        writer.WriteUInt16(value);

        var bytes = writer.ToArray();
        Assert.Equal(2, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadUInt16LittleEndian(bytes));
    }

    // --- WriteInt32 / WriteUInt32 ---

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void WriteInt32_WritesLittleEndian(int value)
    {
        var writer = new DBusWireWriter();
        writer.WriteInt32(value);

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadInt32LittleEndian(bytes));
    }

    [Fact]
    public void WriteInt32_PadsTo4ByteAlignment()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x01); // pos 1
        writer.WriteByte(0x02); // pos 2
        writer.WriteInt32(42); // should pad to pos 4

        var bytes = writer.ToArray();
        Assert.Equal(8, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        Assert.Equal(0x02, bytes[1]);
        Assert.Equal(0, bytes[2]); // pad
        Assert.Equal(0, bytes[3]); // pad
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(uint.MaxValue)]
    public void WriteUInt32_WritesLittleEndian(uint value)
    {
        var writer = new DBusWireWriter();
        writer.WriteUInt32(value);

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
    }

    // --- WriteInt64 / WriteUInt64 ---

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void WriteInt64_WritesLittleEndian(long value)
    {
        var writer = new DBusWireWriter();
        writer.WriteInt64(value);

        var bytes = writer.ToArray();
        Assert.Equal(8, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadInt64LittleEndian(bytes));
    }

    [Fact]
    public void WriteInt64_PadsTo8ByteAlignment()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x01); // pos 1
        writer.WriteInt64(123456789L); // should pad to pos 8

        var bytes = writer.ToArray();
        Assert.Equal(16, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        // bytes 1-7: padding
        for (int i = 1; i < 8; i++)
            Assert.Equal(0, bytes[i]);
        Assert.Equal(123456789L, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8)));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(ulong.MaxValue)]
    public void WriteUInt64_WritesLittleEndian(ulong value)
    {
        var writer = new DBusWireWriter();
        writer.WriteUInt64(value);

        var bytes = writer.ToArray();
        Assert.Equal(8, bytes.Length);
        Assert.Equal(value, BinaryPrimitives.ReadUInt64LittleEndian(bytes));
    }

    // --- WriteDouble ---

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-1.0)]
    [InlineData(3.14159265358979)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void WriteDouble_WritesLittleEndian(double value)
    {
        var writer = new DBusWireWriter();
        writer.WriteDouble(value);

        var bytes = writer.ToArray();
        Assert.Equal(8, bytes.Length);
        var bits = BinaryPrimitives.ReadInt64LittleEndian(bytes);
        Assert.Equal(value, BitConverter.Int64BitsToDouble(bits));
    }

    [Fact]
    public void WriteDouble_PadsTo8ByteAlignment()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x01);
        writer.WriteDouble(1.0);

        var bytes = writer.ToArray();
        Assert.Equal(16, bytes.Length);
        // bytes 1-7 are padding
        for (int i = 1; i < 8; i++)
            Assert.Equal(0, bytes[i]);
    }

    // --- WriteString ---

    [Fact]
    public void WriteString_EmptyString()
    {
        var writer = new DBusWireWriter();
        writer.WriteString("");

        var bytes = writer.ToArray();
        // 4 bytes length (0) + 1 byte null terminator = 5
        Assert.Equal(5, bytes.Length);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(0, bytes[4]); // null terminator
    }

    [Fact]
    public void WriteString_AsciiString()
    {
        var writer = new DBusWireWriter();
        writer.WriteString("Hello");

        var bytes = writer.ToArray();
        // 4 bytes length + 5 bytes "Hello" + 1 null = 10
        Assert.Equal(10, bytes.Length);
        Assert.Equal(5u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal("Hello", Encoding.UTF8.GetString(bytes, 4, 5));
        Assert.Equal(0, bytes[9]); // null terminator
    }

    [Fact]
    public void WriteString_Utf8MultiByte()
    {
        var writer = new DBusWireWriter();
        writer.WriteString("\u00e9"); // e-acute, 2 UTF-8 bytes

        var bytes = writer.ToArray();
        // 4 bytes length (2) + 2 UTF-8 bytes + 1 null = 7
        Assert.Equal(7, bytes.Length);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal(0, bytes[6]); // null terminator
    }

    [Fact]
    public void WriteString_PadsTo4ByteAlignmentForLengthPrefix()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x01); // pos 1
        writer.WriteString("AB"); // length prefix pads to pos 4

        var bytes = writer.ToArray();
        // 1 byte + 3 pad + 4 length + 2 chars + 1 null = 11
        Assert.Equal(11, bytes.Length);
        Assert.Equal(0x01, bytes[0]);
        // bytes 1-3: padding for uint32 alignment
        Assert.Equal(0, bytes[1]);
        Assert.Equal(0, bytes[2]);
        Assert.Equal(0, bytes[3]);
        // bytes 4-7: length = 2
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));
        // bytes 8-9: "AB"
        Assert.Equal((byte)'A', bytes[8]);
        Assert.Equal((byte)'B', bytes[9]);
        // byte 10: null terminator
        Assert.Equal(0, bytes[10]);
    }

    // --- WriteObjectPath ---

    [Fact]
    public void WriteObjectPath_SameFormatAsString()
    {
        var writerString = new DBusWireWriter();
        writerString.WriteString("/org/freedesktop");

        var writerPath = new DBusWireWriter();
        writerPath.WriteObjectPath("/org/freedesktop");

        Assert.Equal(writerString.ToArray(), writerPath.ToArray());
    }

    // --- WriteSignature ---

    [Fact]
    public void WriteSignature_EmptySignature()
    {
        var writer = new DBusWireWriter();
        writer.WriteSignature("");

        var bytes = writer.ToArray();
        // 1 byte length (0) + 1 byte null terminator = 2
        Assert.Equal(2, bytes.Length);
        Assert.Equal(0, bytes[0]); // length
        Assert.Equal(0, bytes[1]); // null terminator
    }

    [Fact]
    public void WriteSignature_SimpleSignature()
    {
        var writer = new DBusWireWriter();
        writer.WriteSignature("su");

        var bytes = writer.ToArray();
        // 1 byte length (2) + 2 bytes "su" + 1 null = 4
        Assert.Equal(4, bytes.Length);
        Assert.Equal(2, bytes[0]); // length byte
        Assert.Equal((byte)'s', bytes[1]);
        Assert.Equal((byte)'u', bytes[2]);
        Assert.Equal(0, bytes[3]); // null terminator
    }

    [Fact]
    public void WriteSignature_HasNoPadding()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0xFF); // position 1
        writer.WriteSignature("i"); // no padding, immediately at position 1

        var bytes = writer.ToArray();
        // 1 byte (0xFF) + 1 length + 1 'i' + 1 null = 4
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(1, bytes[1]); // length byte
        Assert.Equal((byte)'i', bytes[2]);
        Assert.Equal(0, bytes[3]); // null terminator
    }

    // --- WritePad ---

    [Fact]
    public void WritePad_AlreadyAligned_NoChange()
    {
        var writer = new DBusWireWriter();
        writer.WriteInt32(0); // position 4, already aligned to 4
        var positionBefore = writer.Position;
        writer.WritePad(4);
        Assert.Equal(positionBefore, writer.Position);
    }

    [Fact]
    public void WritePad_WritesZeroBytes()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0xFF); // position 1
        writer.WritePad(4); // should advance to position 4

        var bytes = writer.ToArray();
        Assert.Equal(4, bytes.Length);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0, bytes[1]);
        Assert.Equal(0, bytes[2]);
        Assert.Equal(0, bytes[3]);
    }

    [Theory]
    [InlineData(1, 8, 8)] // 1 byte written, pad to 8 -> pos 8
    [InlineData(2, 4, 4)] // 2 bytes written, pad to 4 -> pos 4
    [InlineData(3, 2, 4)] // 3 bytes written, pad to 2 -> pos 4
    [InlineData(4, 4, 4)] // 4 bytes written, pad to 4 -> already aligned, pos 4
    [InlineData(5, 8, 8)] // 5 bytes written, pad to 8 -> pos 8
    public void WritePad_AdvancesToCorrectPosition(int bytesWritten, int alignment, int expectedPosition)
    {
        var writer = new DBusWireWriter();
        for (int i = 0; i < bytesWritten; i++)
            writer.WriteByte(0xAA);

        writer.WritePad(alignment);

        Assert.Equal(expectedPosition, writer.Position);
    }

    // --- WriteNull ---

    [Fact]
    public void WriteNull_WritesSingleZeroByte()
    {
        var writer = new DBusWireWriter();
        writer.WriteNull();

        var bytes = writer.ToArray();
        Assert.Single(bytes);
        Assert.Equal(0, bytes[0]);
    }

    // --- Position / SetPosition ---

    [Fact]
    public void Position_TracksCurrentWriteOffset()
    {
        var writer = new DBusWireWriter();
        Assert.Equal(0, writer.Position);

        writer.WriteByte(0x01);
        Assert.Equal(1, writer.Position);

        writer.WriteInt32(42);
        // 1 byte + 3 pad + 4 int32 = 8
        Assert.Equal(8, writer.Position);
    }

    [Fact]
    public void SetPosition_SeeksToPosition()
    {
        var writer = new DBusWireWriter();
        writer.WriteInt32(0); // placeholder
        writer.WriteInt32(99);

        // Go back and overwrite the first int32
        writer.SetPosition(0);
        writer.WriteInt32(42);

        var bytes = writer.ToArray();
        Assert.Equal(42, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0)));
        Assert.Equal(99, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
    }

    [Fact]
    public void SetPosition_UsedForBackpatchingArrayLength()
    {
        // Simulate the array-length backpatching pattern:
        // 1. Write placeholder uint32 for array length
        // 2. Write array elements
        // 3. Compute length, seek back, write real length, seek forward
        var writer = new DBusWireWriter();
        long lengthPos = writer.Position;
        writer.WriteUInt32(0); // placeholder

        long startPos = writer.Position;
        writer.WriteInt32(1);
        writer.WriteInt32(2);
        writer.WriteInt32(3);
        long endPos = writer.Position;

        uint arrayLength = (uint)(endPos - startPos);
        writer.SetPosition(lengthPos);
        writer.WriteUInt32(arrayLength); // backpatch
        writer.SetPosition(endPos);

        var bytes = writer.ToArray();
        Assert.Equal(16, bytes.Length); // 4 (length) + 3*4 (elements)
        Assert.Equal(12u, BinaryPrimitives.ReadUInt32LittleEndian(bytes)); // array length = 12
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)));
    }

    // --- ToArray ---

    [Fact]
    public void ToArray_EmptyWriter_ReturnsEmptyArray()
    {
        var writer = new DBusWireWriter();
        Assert.Empty(writer.ToArray());
    }

    [Fact]
    public void ToArray_ReturnsAllWrittenBytes()
    {
        var writer = new DBusWireWriter();
        writer.WriteByte(0x01);
        writer.WriteByte(0x02);
        writer.WriteByte(0x03);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, writer.ToArray());
    }

    // --- Combined / integration-style tests ---

    [Fact]
    public void WriteMultiplePrimitives_AlignmentIsCorrect()
    {
        // Simulate writing: byte, int32, byte, int64
        var writer = new DBusWireWriter();
        writer.WriteByte(0xAA);    // pos 0, size 1 -> pos 1
        writer.WriteInt32(0x1234); // pad to 4, write 4 -> pos 8
        writer.WriteByte(0xBB);    // pos 8, size 1 -> pos 9
        writer.WriteInt64(0x5678); // pad to 16, write 8 -> pos 24

        var bytes = writer.ToArray();
        Assert.Equal(24, bytes.Length);

        Assert.Equal(0xAA, bytes[0]);
        Assert.Equal(0x1234, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)));
        Assert.Equal(0xBB, bytes[8]);
        Assert.Equal(0x5678L, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(16)));
    }

    [Fact]
    public void WriteStringThenSignature_CorrectLayout()
    {
        var writer = new DBusWireWriter();
        writer.WriteString("Hi");
        // After string: 4 (len) + 2 (Hi) + 1 (null) = 7 bytes at pos 7
        writer.WriteSignature("s");
        // Signature: no padding. 1 (len) + 1 (s) + 1 (null) = 3 bytes

        var bytes = writer.ToArray();
        Assert.Equal(10, bytes.Length);

        // String part
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        Assert.Equal((byte)'H', bytes[4]);
        Assert.Equal((byte)'i', bytes[5]);
        Assert.Equal(0, bytes[6]);

        // Signature part (starts at 7)
        Assert.Equal(1, bytes[7]); // signature length
        Assert.Equal((byte)'s', bytes[8]);
        Assert.Equal(0, bytes[9]); // null terminator
    }
}
