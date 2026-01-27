using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[StructLayout(LayoutKind.Explicit)]
internal unsafe struct DBusBasicValue
{
    [FieldOffset(0)]
    [NativeTypeName("unsigned char[8]")]
    public fixed byte bytes[8];

    [FieldOffset(0)]
    [NativeTypeName("dbus_int16_t")]
    public short i16;

    [FieldOffset(0)]
    [NativeTypeName("dbus_uint16_t")]
    public ushort u16;

    [FieldOffset(0)]
    [NativeTypeName("dbus_int32_t")]
    public int i32;

    [FieldOffset(0)]
    [NativeTypeName("dbus_uint32_t")]
    public uint u32;

    [FieldOffset(0)]
    [NativeTypeName("dbus_bool_t")]
    public uint bool_val;

    [FieldOffset(0)]
    [NativeTypeName("dbus_int64_t")]
    public IntPtr i64;

    [FieldOffset(0)]
    [NativeTypeName("dbus_uint64_t")]
    public UIntPtr u64;

    [FieldOffset(0)]
    public DBus8ByteStruct eight;

    [FieldOffset(0)]
    public double dbl;

    [FieldOffset(0)]
    [NativeTypeName("unsigned char")]
    public byte byt;

    [FieldOffset(0)]
    [NativeTypeName("char *")]
    public byte* str;

    [FieldOffset(0)]
    public int fd;
}