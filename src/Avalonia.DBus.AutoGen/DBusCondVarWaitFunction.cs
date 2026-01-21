using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void DBusCondVarWaitFunction(DBusCondVar* cond, DBusMutex* mutex);