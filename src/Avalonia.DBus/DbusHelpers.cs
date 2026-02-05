using System;
using System.Runtime.InteropServices;
using System.Threading;
using LibDbus = Avalonia.DBus.Native.LibDbus;

namespace Avalonia.DBus;

internal static unsafe class DbusHelpers
{
    private static int s_threadsInitialized;

    public static string PtrToString(byte* value)
        => value == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)value) ?? string.Empty;

    public static string? PtrToStringNullable(byte* value)
        => value == null ? null : Marshal.PtrToStringUTF8((IntPtr)value);

    public static void EnsureThreadsInitialized()
    {
        if (Interlocked.Exchange(ref s_threadsInitialized, 1) == 0)
        {
            LibDbus.dbus_threads_init_default();
        }
    }
}
