using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusRecursiveMutexLockFunction(DBusMutex* mutex);