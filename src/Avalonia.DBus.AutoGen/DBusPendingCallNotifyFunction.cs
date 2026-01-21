using System.Runtime.InteropServices;

namespace Avalonia.DBus.AutoGen;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public unsafe delegate void DBusPendingCallNotifyFunction(DBusPendingCall* pending, void* user_data);