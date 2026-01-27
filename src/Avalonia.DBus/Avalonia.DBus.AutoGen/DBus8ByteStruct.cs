namespace Avalonia.DBus.AutoGen;

internal struct DBus8ByteStruct
{
    [NativeTypeName("dbus_uint32_t")]
    public uint first32;

    [NativeTypeName("dbus_uint32_t")]
    public uint second32;
}