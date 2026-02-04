using System.Runtime.InteropServices;

namespace Avalonia.DBus;

internal static unsafe class NativeMethods
{
    [DllImport("libdbus-1.so.3", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    public static extern void dbus_free(void* memory);
}
