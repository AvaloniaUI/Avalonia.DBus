using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void DBusMutexFreeFunction(DBusMutex* mutex);