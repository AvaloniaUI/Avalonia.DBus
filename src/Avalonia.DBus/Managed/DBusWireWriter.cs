using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Avalonia.DBus.Managed;

/// <summary>
/// Writes CLR primitives into D-Bus wire format bytes (little-endian).
/// This is the serialize half of the managed marshaller.
/// </summary>
internal sealed class DBusWireWriter
{
    private readonly MemoryStream _stream = new();

    /// <summary>
    /// Gets the current write position in the stream.
    /// </summary>
    public long Position => _stream.Position;

    /// <summary>
    /// Seeks to the specified position. Used for backpatching array lengths.
    /// </summary>
    public void SetPosition(long position) => _stream.Position = position;

    /// <summary>
    /// Returns all written bytes as a byte array.
    /// </summary>
    public byte[] ToArray() => _stream.ToArray();

    /// <summary>
    /// Writes a single byte. No alignment required.
    /// </summary>
    public void WriteByte(byte value)
    {
        _stream.WriteByte(value);
    }

    /// <summary>
    /// Writes a boolean as a uint32 (0 or 1), 4-byte aligned.
    /// </summary>
    public void WriteBoolean(bool value)
    {
        WriteUInt32(value ? 1u : 0u);
    }

    /// <summary>
    /// Writes a signed 16-bit integer, 2-byte aligned, little-endian.
    /// </summary>
    public void WriteInt16(short value)
    {
        WritePad(2);
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer, 2-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt16(ushort value)
    {
        WritePad(2);
        Span<byte> buf = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes a signed 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public void WriteInt32(int value)
    {
        WritePad(4);
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt32(uint value)
    {
        WritePad(4);
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes a signed 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public void WriteInt64(long value)
    {
        WritePad(8);
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt64(ulong value)
    {
        WritePad(8);
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes a double as 8 bytes, 8-byte aligned, little-endian.
    /// Uses BitConverter.DoubleToInt64Bits then writes as int64.
    /// </summary>
    public void WriteDouble(double value)
    {
        WriteInt64(BitConverter.DoubleToInt64Bits(value));
    }

    /// <summary>
    /// Writes a D-Bus STRING: 4-byte aligned uint32 length prefix, UTF-8 bytes, null terminator.
    /// </summary>
    public void WriteString(string value)
    {
        byte[] utf8Data = Encoding.UTF8.GetBytes(value);
        WriteUInt32((uint)utf8Data.Length);
        _stream.Write(utf8Data, 0, utf8Data.Length);
        WriteNull();
    }

    /// <summary>
    /// Writes a D-Bus OBJECT_PATH: same wire format as STRING.
    /// </summary>
    public void WriteObjectPath(string value)
    {
        WriteString(value);
    }

    /// <summary>
    /// Writes a D-Bus SIGNATURE: byte length prefix (NOT uint32), ASCII bytes, null terminator.
    /// No alignment padding.
    /// </summary>
    public void WriteSignature(string value)
    {
        byte[] asciiData = Encoding.ASCII.GetBytes(value);
        WriteByte((byte)asciiData.Length);
        _stream.Write(asciiData, 0, asciiData.Length);
        WriteNull();
    }

    /// <summary>
    /// Advances the write position to the next multiple of the specified alignment,
    /// writing zero bytes for padding.
    /// </summary>
    public void WritePad(int alignment)
    {
        int pos = (int)_stream.Position;
        int pad = pos % alignment;
        if (pad != 0)
        {
            int needed = alignment - pad;
            for (int i = 0; i < needed; i++)
                _stream.WriteByte(0);
        }
    }

    /// <summary>
    /// Writes a single zero byte.
    /// </summary>
    public void WriteNull()
    {
        _stream.WriteByte(0);
    }
}
