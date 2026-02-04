namespace Avalonia.DBus.Native;

internal unsafe struct DBusError
{
    [NativeTypeName("const char *")]
    public byte* name;

    [NativeTypeName("const char *")]
    public byte* message;

    public uint _bitfield;

    [NativeTypeName("unsigned int : 1")]
    public uint dummy1
    {
        get
        {
            return _bitfield & 0x1u;
        }

        set
        {
            _bitfield = (_bitfield & ~0x1u) | (value & 0x1u);
        }
    }

    [NativeTypeName("unsigned int : 1")]
    public uint dummy2
    {
        get
        {
            return (_bitfield >> 1) & 0x1u;
        }

        set
        {
            _bitfield = (_bitfield & ~(0x1u << 1)) | ((value & 0x1u) << 1);
        }
    }

    [NativeTypeName("unsigned int : 1")]
    public uint dummy3
    {
        get
        {
            return (_bitfield >> 2) & 0x1u;
        }

        set
        {
            _bitfield = (_bitfield & ~(0x1u << 2)) | ((value & 0x1u) << 2);
        }
    }

    [NativeTypeName("unsigned int : 1")]
    public uint dummy4
    {
        get
        {
            return (_bitfield >> 3) & 0x1u;
        }

        set
        {
            _bitfield = (_bitfield & ~(0x1u << 3)) | ((value & 0x1u) << 3);
        }
    }

    [NativeTypeName("unsigned int : 1")]
    public uint dummy5
    {
        get
        {
            return (_bitfield >> 4) & 0x1u;
        }

        set
        {
            _bitfield = (_bitfield & ~(0x1u << 4)) | ((value & 0x1u) << 4);
        }
    }

    public void* padding1;
}