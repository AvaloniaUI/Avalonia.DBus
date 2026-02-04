using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusCondVarWaitFunction(DBusCondVar* cond, DBusMutex* mutex);