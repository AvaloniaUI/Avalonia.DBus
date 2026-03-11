using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace Avalonia.DBus.Managed;

/// <summary>
/// Reads CLR primitives from D-Bus wire format bytes.
/// This is the deserialize half of the managed marshaller — the inverse of <see cref="DBusWireWriter"/>.
/// Supports both little-endian and big-endian byte order.
/// </summary>
internal sealed class DBusWireReader
{
    private const int ByteSize = sizeof(byte);
    private const int Int16Size = sizeof(short);
    private const int UInt16Size = sizeof(ushort);
    private const int Int32Size = sizeof(int);
    private const int UInt32Size = sizeof(uint);
    private const int Int64Size = sizeof(long);
    private const int UInt64Size = sizeof(ulong);

    private readonly byte[] _data;
    private readonly bool _bigEndian;
    private int _pos;

    /// <summary>
    /// Creates a new reader over the given byte array.
    /// </summary>
    /// <param name="data">The raw bytes to read from.</param>
    /// <param name="bigEndian">If true, reads multi-byte values as big-endian; otherwise little-endian.</param>
    public DBusWireReader(byte[] data, bool bigEndian = false)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _bigEndian = bigEndian;
    }

    /// <summary>
    /// Gets or sets the current read position.
    /// </summary>
    public int Position
    {
        get => _pos;
        set
        {
            if ((uint)value > (uint)_data.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"Position {value} is outside buffer bounds [0, {_data.Length}].");
            }

            _pos = value;
        }
    }

    /// <summary>
    /// Total buffer length.
    /// </summary>
    public int Length => _data.Length;

    /// <summary>
    /// Reads a single byte. No alignment required.
    /// </summary>
    public byte ReadByte()
    {
        EnsureCanRead(ByteSize);
        return _data[_pos++];
    }

    /// <summary>
    /// Reads a boolean from a uint32 (4-byte aligned). Returns true if non-zero.
    /// </summary>
    public bool ReadBoolean()
    {
        return ReadUInt32() != 0;
    }

    /// <summary>
    /// Reads a 2-byte aligned signed 16-bit integer using the configured byte order.
    /// </summary>
    public short ReadInt16()
    {
        ReadPad(Int16Size);
        EnsureCanRead(Int16Size);
        var span = _data.AsSpan(_pos, Int16Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadInt16BigEndian(span)
            : BinaryPrimitives.ReadInt16LittleEndian(span);
        _pos += Int16Size;
        return value;
    }

    /// <summary>
    /// Reads a 2-byte aligned unsigned 16-bit integer using the configured byte order.
    /// </summary>
    public ushort ReadUInt16()
    {
        ReadPad(UInt16Size);
        EnsureCanRead(UInt16Size);
        var span = _data.AsSpan(_pos, UInt16Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadUInt16BigEndian(span)
            : BinaryPrimitives.ReadUInt16LittleEndian(span);
        _pos += UInt16Size;
        return value;
    }

    /// <summary>
    /// Reads a 4-byte aligned signed 32-bit integer using the configured byte order.
    /// </summary>
    public int ReadInt32()
    {
        ReadPad(Int32Size);
        EnsureCanRead(Int32Size);
        var span = _data.AsSpan(_pos, Int32Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadInt32BigEndian(span)
            : BinaryPrimitives.ReadInt32LittleEndian(span);
        _pos += Int32Size;
        return value;
    }

    /// <summary>
    /// Reads a 4-byte aligned unsigned 32-bit integer using the configured byte order.
    /// </summary>
    public uint ReadUInt32()
    {
        ReadPad(UInt32Size);
        EnsureCanRead(UInt32Size);
        var span = _data.AsSpan(_pos, UInt32Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadUInt32BigEndian(span)
            : BinaryPrimitives.ReadUInt32LittleEndian(span);
        _pos += UInt32Size;
        return value;
    }

    /// <summary>
    /// Reads an 8-byte aligned signed 64-bit integer using the configured byte order.
    /// </summary>
    public long ReadInt64()
    {
        ReadPad(Int64Size);
        EnsureCanRead(Int64Size);
        var span = _data.AsSpan(_pos, Int64Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadInt64BigEndian(span)
            : BinaryPrimitives.ReadInt64LittleEndian(span);
        _pos += Int64Size;
        return value;
    }

    /// <summary>
    /// Reads an 8-byte aligned unsigned 64-bit integer using the configured byte order.
    /// </summary>
    public ulong ReadUInt64()
    {
        ReadPad(UInt64Size);
        EnsureCanRead(UInt64Size);
        var span = _data.AsSpan(_pos, UInt64Size);
        var value = _bigEndian
            ? BinaryPrimitives.ReadUInt64BigEndian(span)
            : BinaryPrimitives.ReadUInt64LittleEndian(span);
        _pos += UInt64Size;
        return value;
    }

    /// <summary>
    /// Reads an 8-byte aligned IEEE 754 double using the configured byte order.
    /// Reads as int64 and converts via <see cref="BitConverter.Int64BitsToDouble"/>.
    /// </summary>
    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadInt64());
    }

    /// <summary>
    /// Reads a D-Bus STRING: uint32 length prefix (4-byte aligned), UTF-8 bytes, null terminator.
    /// </summary>
    public string ReadString()
    {
        var length = ReadUInt32();

        if (length > int.MaxValue)
        {
            throw new InvalidDataException($"String length {length} exceeds supported range.");
        }

        var lengthInt = (int)length;
        EnsureCanRead(lengthInt);
        var value = Encoding.UTF8.GetString(_data, _pos, lengthInt);
        _pos += lengthInt;
        ReadNull();
        return value;
    }

    /// <summary>
    /// Reads a D-Bus OBJECT_PATH: same wire format as STRING.
    /// </summary>
    public string ReadObjectPath()
    {
        return ReadString();
    }

    /// <summary>
    /// Reads a D-Bus SIGNATURE: byte length prefix (NOT uint32), ASCII bytes, null terminator.
    /// No alignment padding.
    /// </summary>
    public string ReadSignature()
    {
        var length = ReadByte();
        EnsureCanRead(length);
        var value = Encoding.ASCII.GetString(_data, _pos, length);
        _pos += length;
        ReadNull();
        return value;
    }

    /// <summary>
    /// Advances the read position to the next multiple of the specified alignment.
    /// </summary>
    public void ReadPad(int alignment)
    {
        if (alignment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Alignment must be greater than 0.");
        }

        var pad = _pos % alignment;
        if (pad != 0)
        {
            var advance = alignment - pad;
            EnsureCanRead(advance);
            _pos += advance;
        }
    }

    /// <summary>
    /// Reads and verifies a single zero byte (null terminator).
    /// </summary>
    public void ReadNull()
    {
        EnsureCanRead(ByteSize);
        if (_data[_pos] != 0)
            throw new InvalidOperationException(
                $"Read non-zero byte 0x{_data[_pos]:X2} at position {_pos} while expecting null terminator");
        _pos++;
    }

    private void EnsureCanRead(int byteCount)
    {
        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount), byteCount, "Byte count cannot be negative.");
        }

        var end = (long)_pos + byteCount;
        if (end > _data.Length)
        {
            throw new InvalidDataException(
                $"Attempted to read {byteCount} bytes at position {_pos} from buffer of length {_data.Length}.");
        }
    }
}
