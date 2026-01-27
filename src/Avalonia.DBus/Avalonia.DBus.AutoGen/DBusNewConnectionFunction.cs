using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusNewConnectionFunction(DBusServer* server, DBusConnection* new_connection, void* data);