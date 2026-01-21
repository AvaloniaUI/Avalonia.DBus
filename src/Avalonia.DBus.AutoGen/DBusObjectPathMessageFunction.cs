using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate DBusHandlerResult DBusObjectPathMessageFunction(DBusConnection* connection, DBusMessage* message, void* user_data);