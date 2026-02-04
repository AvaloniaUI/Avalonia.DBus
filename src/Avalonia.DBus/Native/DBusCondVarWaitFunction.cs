using System.Runtime.InteropServices;

namespace Avalonia.DBus.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusCondVarWaitFunction(DBusCondVar* cond, DBusMutex* mutex);