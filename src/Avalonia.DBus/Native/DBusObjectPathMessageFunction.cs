using System.Runtime.InteropServices;

namespace Avalonia.DBus.Native;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate DBusHandlerResult DBusObjectPathMessageFunction(DBusConnection* connection, DBusMessage* message, void* user_data);