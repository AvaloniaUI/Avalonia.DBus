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
    private const int Int16Size = sizeof(short);
    private const int UInt16Size = sizeof(ushort);
    private const int Int32Size = sizeof(int);
    private const int UInt32Size = sizeof(uint);
    private const int Int64Size = sizeof(long);
    private const int UInt64Size = sizeof(ulong);

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
        WritePad(Int16Size);
        Span<byte> buf = stackalloc byte[Int16Size];
        BinaryPrimitives.WriteInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 16-bit integer, 2-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt16(ushort value)
    {
        WritePad(UInt16Size);
        Span<byte> buf = stackalloc byte[UInt16Size];
        BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes a signed 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public void WriteInt32(int value)
    {
        WritePad(Int32Size);
        Span<byte> buf = stackalloc byte[Int32Size];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt32(uint value)
    {
        WritePad(UInt32Size);
        Span<byte> buf = stackalloc byte[UInt32Size];
        BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes a signed 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public void WriteInt64(long value)
    {
        WritePad(Int64Size);
        Span<byte> buf = stackalloc byte[Int64Size];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        _stream.Write(buf);
    }

    /// <summary>
    /// Writes an unsigned 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public void WriteUInt64(ulong value)
    {
        WritePad(UInt64Size);
        Span<byte> buf = stackalloc byte[UInt64Size];
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
        ArgumentNullException.ThrowIfNull(value);
        var utf8Data = Encoding.UTF8.GetBytes(value);
        WriteUInt32((uint)utf8Data.Length);
        _stream.Write(utf8Data, 0, utf8Data.Length);
        WriteNull();
    }

    /// <summary>
    /// Writes a D-Bus OBJECT_PATH: same wire format as STRING.
    /// </summary>
    public void WriteObjectPath(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteString(value);
    }

    /// <summary>
    /// Writes a D-Bus SIGNATURE: byte length prefix (NOT uint32), ASCII bytes, null terminator.
    /// No alignment padding.
    /// </summary>
    public void WriteSignature(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var asciiData = Encoding.ASCII.GetBytes(value);
        if (asciiData.Length > byte.MaxValue)
        {
            throw new InvalidOperationException(
                $"D-Bus signature length {asciiData.Length} exceeds max {byte.MaxValue} bytes.");
        }

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
        if (alignment <= 0)
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be greater than 0.");

        var pos = checked((int)_stream.Position);
        var pad = pos % alignment;
        if (pad != 0)
        {
            var needed = alignment - pad;
            for (var i = 0; i < needed; i++)
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
