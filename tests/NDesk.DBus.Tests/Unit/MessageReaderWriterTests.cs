using System;
using System.Text;
using Xunit;

namespace NDesk.DBus.Tests.Unit;

public class MessageReaderWriterTests
{
    private static byte[] WriteAndGetBytes(Action<MessageWriter> write)
    {
        var writer = new MessageWriter(Connection.NativeEndianness);
        write(writer);
        return writer.ToArray();
    }

    private static T RoundTrip<T>(Action<MessageWriter> write, Func<MessageReader, T> read)
    {
        var data = WriteAndGetBytes(write);
        var reader = new MessageReader(Connection.NativeEndianness, data);
        return read(reader);
    }

    [Fact]
    public void AllTypes_InSequence_RoundTrip()
    {
        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write((byte)0xAB);
        writer.Write(true);
        writer.Write((short)-500);
        writer.Write((ushort)60000);
        writer.Write(-123456);
        writer.Write((uint)123456);
        writer.Write(-9876543210L);
        writer.Write(9876543210UL);
        writer.Write(1.5f);
        writer.Write(2.718281828);
        writer.Write("test string");
        writer.Write(new ObjectPath("/com/example/Test"));
        writer.Write(new Signature("a{sv}"));
        var data = writer.ToArray();

        var reader = new MessageReader(Connection.NativeEndianness, data);

        Assert.Equal(0xAB, reader.ReadByte());
        Assert.True(reader.ReadBoolean());
        Assert.Equal((short)-500, reader.ReadInt16());
        Assert.Equal((ushort)60000, reader.ReadUInt16());
        Assert.Equal(-123456, reader.ReadInt32());
        Assert.Equal(123456u, reader.ReadUInt32());
        Assert.Equal(-9876543210L, reader.ReadInt64());
        Assert.Equal(9876543210UL, reader.ReadUInt64());
        Assert.Equal(1.5f, reader.ReadSingle());
        Assert.Equal(2.718281828, reader.ReadDouble());
        Assert.Equal("test string", reader.ReadString());
        Assert.Equal(new ObjectPath("/com/example/Test"), reader.ReadObjectPath());
        Assert.Equal("a{sv}", reader.ReadSignature().Value);
    }

    [Fact]
    public void ByteThenInt32_PaddingInserted()
    {
        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write((byte)0xAB);
        writer.Write(42);
        var data = writer.ToArray();

        Assert.Equal(8, data.Length);

        var reader = new MessageReader(Connection.NativeEndianness, data);
        Assert.Equal(0xAB, reader.ReadByte());
        Assert.Equal(42, reader.ReadInt32());
    }

    [Fact]
    public void String_Then_Double_AlignmentCorrect()
    {
        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write("hi");
        writer.Write(99.99);
        var data = writer.ToArray();

        Assert.Equal(16, data.Length);

        var reader = new MessageReader(Connection.NativeEndianness, data);
        Assert.Equal("hi", reader.ReadString());
        Assert.Equal(99.99, reader.ReadDouble());
    }

    [Fact]
    public void String_Utf8Characters_RoundTrips()
    {
        var value = "h\u00e9llo";
        var result = RoundTrip(w => w.Write(value), r => r.ReadString());

        Assert.Equal(value, result);
    }

    [Fact]
    public void Boolean_True_WireFormat_IsUint32One()
    {
        var data = WriteAndGetBytes(w => w.Write(true));

        var reader = new MessageReader(Connection.NativeEndianness, data);
        var raw = reader.ReadUInt32();

        Assert.Equal(1u, raw);
    }

    [Fact]
    public void Signature_WireFormat_LengthIsSingleByte()
    {
        var data = WriteAndGetBytes(w => w.Write(new Signature("si")));

        Assert.Equal(new byte[] { 2, (byte)'s', (byte)'i', 0 }, data);
    }

    [Fact]
    public void String_Utf8_WireFormat_LengthIsByteCount()
    {
        var value = "h\u00e9llo";
        var data = WriteAndGetBytes(w => w.Write(value));

        var reader = new MessageReader(Connection.NativeEndianness, data);
        var length = reader.ReadUInt32();

        var expectedByteCount = Encoding.UTF8.GetByteCount(value);
        Assert.Equal((uint)expectedByteCount, length);
        Assert.Equal(6u, length);
    }

    [Fact]
    public void Padding_Bytes_AreZero()
    {
        var writer = new MessageWriter(Connection.NativeEndianness);
        writer.Write((byte)0xFF);
        writer.Write(42);
        var data = writer.ToArray();

        Assert.All(data[1..4], b => Assert.Equal(0, b));
    }
}
