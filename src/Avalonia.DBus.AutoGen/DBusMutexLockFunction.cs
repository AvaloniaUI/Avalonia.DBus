using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: NativeTypeName("dbus_bool_t")]
public unsafe delegate uint DBusMutexLockFunction(DBusMutex* mutex);