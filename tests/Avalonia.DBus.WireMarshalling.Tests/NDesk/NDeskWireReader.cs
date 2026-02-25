using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

internal sealed class NDeskWireReader : IWireReader
{
    private readonly MessageReader _reader;

    public NDeskWireReader(EndianFlag endianness, byte[] data)
    {
        _reader = new MessageReader(endianness, data);
    }

    internal NDeskWireReader(MessageReader reader)
    {
        _reader = reader;
    }

    internal MessageReader Inner => _reader;

    public byte ReadByte() => _reader.ReadByte();
    public bool ReadBoolean() => _reader.ReadBoolean();
    public short ReadInt16() => _reader.ReadInt16();
    public ushort ReadUInt16() => _reader.ReadUInt16();
    public int ReadInt32() => _reader.ReadInt32();
    public uint ReadUInt32() => _reader.ReadUInt32();
    public long ReadInt64() => _reader.ReadInt64();
    public ulong ReadUInt64() => _reader.ReadUInt64();
    public double ReadDouble() => _reader.ReadDouble();
    public string ReadString() => _reader.ReadString();
    public string ReadObjectPath() => _reader.ReadObjectPath().ToString();
    public string ReadSignature() => _reader.ReadSignature().Value;

    public object ReadVariant() => _reader.ReadVariant();

    public T[] ReadArray<T>()
    {
        var array = _reader.ReadArray(typeof(T));
        var result = new T[array.Length];
        array.CopyTo(result, 0);
        return result;
    }

    public IDictionary<TKey, TVal> ReadDictionary<TKey, TVal>() where TKey : notnull
    {
        var dict = new Dictionary<TKey, TVal>();
        _reader.GetValueToDict(typeof(TKey), typeof(TVal),
            (System.Collections.IDictionary)dict);
        return dict;
    }

    public int Position => GetPosition();

    private int GetPosition()
    {
        // Access the protected pos field via reflection
        var field = typeof(MessageReader).GetField("pos",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (int)field!.GetValue(_reader)!;
    }
}
