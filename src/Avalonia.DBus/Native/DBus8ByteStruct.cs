namespace Avalonia.DBus.Native;

#pragma warning disable CS0649

internal struct DBus8ByteStruct
{
    [NativeTypeName("dbus_uint32_t")]
    public uint first32;

    [NativeTypeName("dbus_uint32_t")]
    public uint second32;
}
#pragma warning restore CS0649
