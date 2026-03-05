using System;
using System.Buffers.Binary;
using System.Text;

namespace Avalonia.DBus.Managed;

/// <summary>
/// Reads CLR primitives from D-Bus wire format bytes (little-endian).
/// This is the deserialize half of the managed marshaller — the inverse of <see cref="DBusWireWriter"/>.
/// </summary>
internal sealed class DBusWireReader
{
    private readonly byte[] _data;
    private int _pos;

    /// <summary>
    /// Creates a new reader over the given byte array.
    /// </summary>
    public DBusWireReader(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>
    /// Gets or sets the current read position.
    /// </summary>
    public int Position
    {
        get => _pos;
        set => _pos = value;
    }

    /// <summary>
    /// Reads a single byte. No alignment required.
    /// </summary>
    public byte ReadByte()
    {
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
    /// Reads a signed 16-bit integer, 2-byte aligned, little-endian.
    /// </summary>
    public short ReadInt16()
    {
        ReadPad(2);
        var value = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_pos));
        _pos += 2;
        return value;
    }

    /// <summary>
    /// Reads an unsigned 16-bit integer, 2-byte aligned, little-endian.
    /// </summary>
    public ushort ReadUInt16()
    {
        ReadPad(2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_pos));
        _pos += 2;
        return value;
    }

    /// <summary>
    /// Reads a signed 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public int ReadInt32()
    {
        ReadPad(4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_pos));
        _pos += 4;
        return value;
    }

    /// <summary>
    /// Reads an unsigned 32-bit integer, 4-byte aligned, little-endian.
    /// </summary>
    public uint ReadUInt32()
    {
        ReadPad(4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_pos));
        _pos += 4;
        return value;
    }

    /// <summary>
    /// Reads a signed 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public long ReadInt64()
    {
        ReadPad(8);
        var value = BinaryPrimitives.ReadInt64LittleEndian(_data.AsSpan(_pos));
        _pos += 8;
        return value;
    }

    /// <summary>
    /// Reads an unsigned 64-bit integer, 8-byte aligned, little-endian.
    /// </summary>
    public ulong ReadUInt64()
    {
        ReadPad(8);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan(_pos));
        _pos += 8;
        return value;
    }

    /// <summary>
    /// Reads a double (8 bytes, 8-byte aligned, little-endian).
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
        uint length = ReadUInt32();
        var value = Encoding.UTF8.GetString(_data, _pos, (int)length);
        _pos += (int)length;
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
        byte length = ReadByte();
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
        int pad = _pos % alignment;
        if (pad != 0)
        {
            _pos += alignment - pad;
        }
    }

    /// <summary>
    /// Reads and verifies a single zero byte (null terminator).
    /// </summary>
    public void ReadNull()
    {
        if (_data[_pos] != 0)
            throw new InvalidOperationException(
                $"Read non-zero byte 0x{_data[_pos]:X2} at position {_pos} while expecting null terminator");
        _pos++;
    }
}
