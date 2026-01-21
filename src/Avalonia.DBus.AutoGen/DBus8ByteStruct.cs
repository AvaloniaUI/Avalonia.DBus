namespace Avalonia.DBus.AutoGen;

public struct DBus8ByteStruct
{
    [NativeTypeName("dbus_uint32_t")]
    public uint first32;

    [NativeTypeName("dbus_uint32_t")]
    public uint second32;
}