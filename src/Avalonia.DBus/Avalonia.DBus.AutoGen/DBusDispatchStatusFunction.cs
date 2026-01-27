using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void DBusDispatchStatusFunction(DBusConnection* connection, DBusDispatchStatus new_status, void* data);