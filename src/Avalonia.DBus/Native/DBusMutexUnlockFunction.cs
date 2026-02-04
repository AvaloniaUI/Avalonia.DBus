using System.Runtime.InteropServices;

namespace Avalonia.DBus.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: NativeTypeName("dbus_bool_t")]
internal unsafe delegate uint DBusMutexUnlockFunction(DBusMutex* mutex);