using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate DBusMutex* DBusRecursiveMutexNewFunction();