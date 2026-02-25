using System.Collections;
using Avalonia.DBus.WireMarshalling.Tests.Abstractions;
using NDesk.DBus;

namespace Avalonia.DBus.WireMarshalling.Tests.NDesk;

internal sealed class NDeskWireWriter(EndianFlag endianness) : IWireWriter
{
    private readonly MessageWriter _writer = new(endianness);

    internal MessageWriter Inner => _writer;

    public void WriteByte(byte value) => _writer.Write(value);
    public void WriteBoolean(bool value) => _writer.Write(value);
    public void WriteInt16(short value) => _writer.Write(value);
    public void WriteUInt16(ushort value) => _writer.Write(value);
    public void WriteInt32(int value) => _writer.Write(value);
    public void WriteUInt32(uint value) => _writer.Write(value);
    public void WriteInt64(long value) => _writer.Write(value);
    public void WriteUInt64(ulong value) => _writer.Write(value);
    public void WriteDouble(double value) => _writer.Write(value);
    public void WriteString(string value) => _writer.Write(value);
    public void WriteObjectPath(string path) => _writer.Write(new ObjectPath(path));
    public void WriteSignature(string signature) => _writer.Write(new Signature(signature));

    public void WriteVariant(object value) => _writer.Write(value);

    public void WriteArray<T>(T[] values)
    {
        _writer.WriteArray(values, typeof(T));
    }

    public void WriteDictionary<TKey, TVal>(IDictionary<TKey, TVal> dict) where TKey : notnull
    {
        _writer.WriteFromDict(typeof(TKey), typeof(TVal), (IDictionary)dict);
    }

    public void WritePad(int alignment) => _writer.WritePad(alignment);

    public byte[] ToArray() => _writer.ToArray();

    public void Dispose()
    {
        // MessageWriter doesn't implement IDisposable but we conform to the interface
    }
}
