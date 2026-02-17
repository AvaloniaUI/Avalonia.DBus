using System;
using System.Runtime.InteropServices;
using System.Threading;
using LibDbus = Avalonia.DBus.Native.LibDbus;

namespace Avalonia.DBus;

internal static unsafe class DbusHelpers
{
    private static int s_threadsInitialized;
    private const string LibDbusLinuxName = "libdbus-1.so.3";

    public static string PtrToString(byte* value)
        => value == null ? string.Empty : Marshal.PtrToStringUTF8((IntPtr)value) ?? string.Empty;

    public static string? PtrToStringNullable(byte* value)
        => value == null ? null : Marshal.PtrToStringUTF8((IntPtr)value);

    public static void EnsureThreadsInitialized()
    {
        if (Interlocked.Exchange(ref s_threadsInitialized, 1) == 0)
        {
            if (OperatingSystem.IsMacOS())
                RegisterMacOSLibDbusResolver();

            LibDbus.dbus_threads_init_default();
        }
    }

    private static readonly string[] s_macOSLibDbusSearchPaths =
    [
        "/opt/homebrew/lib/libdbus-1.3.dylib",  // Apple Silicon Homebrew
        "/usr/local/lib/libdbus-1.3.dylib",      // Intel Homebrew
        "/opt/local/lib/libdbus-1.3.dylib",      // MacPorts
    ];

    private static void RegisterMacOSLibDbusResolver()
    {
        NativeLibrary.SetDllImportResolver(typeof(LibDbus).Assembly, (name, assembly, searchPath) =>
        {
            if (name != LibDbusLinuxName)
                return IntPtr.Zero;

            foreach (var path in s_macOSLibDbusSearchPaths)
            {
                if (NativeLibrary.TryLoad(path, out var handle))
                    return handle;
            }

            throw new DllNotFoundException(
                "libdbus-1 was not found. Install it with: brew install dbus");
        });
    }
}
