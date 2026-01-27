using System;

#pragma warning disable CS0649

namespace Avalonia.DBus.AutoGen;

internal struct DBusObjectPathVTable
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
#pragma warning restore CS0649
