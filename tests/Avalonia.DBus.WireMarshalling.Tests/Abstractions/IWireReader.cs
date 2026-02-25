namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface IWireReader
{
    byte ReadByte();
    bool ReadBoolean();
    short ReadInt16();
    ushort ReadUInt16();
    int ReadInt32();
    uint ReadUInt32();
    long ReadInt64();
    ulong ReadUInt64();
    double ReadDouble();
    string ReadString();
    string ReadObjectPath();
    string ReadSignature();
    object ReadVariant();
    T[] ReadArray<T>();
    IDictionary<TKey, TVal> ReadDictionary<TKey, TVal>() where TKey : notnull;
    int Position { get; }
}
