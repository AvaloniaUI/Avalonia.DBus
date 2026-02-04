using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: NativeTypeName("dbus_bool_t")]
internal unsafe delegate uint DBusCondVarWaitTimeoutFunction(DBusCondVar* cond, DBusMutex* mutex, int timeout_milliseconds);