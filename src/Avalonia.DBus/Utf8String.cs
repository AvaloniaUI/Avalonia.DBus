using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Avalonia.DBus;

internal sealed unsafe class Utf8String : IDisposable
{
    private readonly byte[] _buffer;
    private GCHandle _handle;

    public Utf8String(string value)
    {
        if (value == null)
        {
            _buffer = [];
            Pointer = null;
            return;
        }

        _buffer = Encoding.UTF8.GetBytes(value + "\0");
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
        Pointer = (byte*)_handle.AddrOfPinnedObject();
    }

    public byte* Pointer { get; }

    public void Dispose()
    {
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}
