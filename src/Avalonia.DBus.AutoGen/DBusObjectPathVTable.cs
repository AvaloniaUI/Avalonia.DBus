using System;

namespace Avalonia.DBus.AutoGen;

public struct DBusObjectPathVTable
{
    [NativeTypeName("DBusObjectPathUnregisterFunction")]
    public IntPtr unregister_function;

    [NativeTypeName("DBusObjectPathMessageFunction")]
    public IntPtr message_function;

    [NativeTypeName("void (*)(void *)")]
    public IntPtr dbus_internal_pad1;

    [NativeTypeName("void (*)(void *)")]
    public IntPtr dbus_internal_pad2;

    [NativeTypeName("void (*)(void *)")]
    public IntPtr dbus_internal_pad3;

    [NativeTypeName("void (*)(void *)")]
    public IntPtr dbus_internal_pad4;
}