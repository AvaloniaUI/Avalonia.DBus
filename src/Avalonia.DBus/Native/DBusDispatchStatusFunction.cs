using System.Runtime.InteropServices;

namespace Avalonia.DBus.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusDispatchStatusFunction(DBusConnection* connection, DBusDispatchStatus new_status, void* data);