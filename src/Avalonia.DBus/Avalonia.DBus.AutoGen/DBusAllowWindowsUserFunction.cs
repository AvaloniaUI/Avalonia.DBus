using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: NativeTypeName("dbus_bool_t")]
internal unsafe delegate uint DBusAllowWindowsUserFunction(DBusConnection* connection, [NativeTypeName("const char *")] byte* user_sid, void* data);