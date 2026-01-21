using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void DBusNewConnectionFunction(DBusServer* server, DBusConnection* new_connection, void* data);