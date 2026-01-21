using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate DBusHandlerResult DBusHandleMessageFunction(DBusConnection* connection, DBusMessage* message, void* user_data);