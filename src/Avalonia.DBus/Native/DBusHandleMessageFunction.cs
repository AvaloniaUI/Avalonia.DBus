using System.Runtime.InteropServices;

namespace Avalonia.DBus.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate DBusHandlerResult DBusHandleMessageFunction(DBusConnection* connection, DBusMessage* message, void* user_data);