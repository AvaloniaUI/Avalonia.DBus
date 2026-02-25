using System.Collections;

namespace Avalonia.DBus.WireMarshalling.Tests.Abstractions;

public interface IWireWriter : IDisposable
{
    void WriteByte(byte value);
    void WriteBoolean(bool value);
    void WriteInt16(short value);
    void WriteUInt16(ushort value);
    void WriteInt32(int value);
    void WriteUInt32(uint value);
    void WriteInt64(long value);
    void WriteUInt64(ulong value);
    void WriteDouble(double value);
    void WriteString(string value);
    void WriteObjectPath(string path);
    void WriteSignature(string signature);
    void WriteVariant(object value);
    void WriteArray<T>(T[] values);
    void WriteDictionary<TKey, TVal>(IDictionary<TKey, TVal> dict) where TKey : notnull;
    void WritePad(int alignment);
    byte[] ToArray();
}
