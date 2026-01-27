using System;

namespace Avalonia.DBus.AutoGen;

public struct DBusThreadFunctions
{
    [NativeTypeName("unsigned int")]
    public uint mask;

    [NativeTypeName("DBusMutexNewFunction")]
    public IntPtr mutex_new;

    [NativeTypeName("DBusMutexFreeFunction")]
    public IntPtr mutex_free;

    [NativeTypeName("DBusMutexLockFunction")]
    public IntPtr mutex_lock;

    [NativeTypeName("DBusMutexUnlockFunction")]
    public IntPtr mutex_unlock;

    [NativeTypeName("DBusCondVarNewFunction")]
    public IntPtr condvar_new;

    [NativeTypeName("DBusCondVarFreeFunction")]
    public IntPtr condvar_free;

    [NativeTypeName("DBusCondVarWaitFunction")]
    public IntPtr condvar_wait;

    [NativeTypeName("DBusCondVarWaitTimeoutFunction")]
    public IntPtr condvar_wait_timeout;

    [NativeTypeName("DBusCondVarWakeOneFunction")]
    public IntPtr condvar_wake_one;

    [NativeTypeName("DBusCondVarWakeAllFunction")]
    public IntPtr condvar_wake_all;

    [NativeTypeName("DBusRecursiveMutexNewFunction")]
    public IntPtr recursive_mutex_new;

    [NativeTypeName("DBusRecursiveMutexFreeFunction")]
    public IntPtr recursive_mutex_free;

    [NativeTypeName("DBusRecursiveMutexLockFunction")]
    public IntPtr recursive_mutex_lock;

    [NativeTypeName("DBusRecursiveMutexUnlockFunction")]
    public IntPtr recursive_mutex_unlock;

    [NativeTypeName("void (*)(void)")]
    public IntPtr padding1;

    [NativeTypeName("void (*)(void)")]
    public IntPtr padding2;

    [NativeTypeName("void (*)(void)")]
    public IntPtr padding3;

    [NativeTypeName("void (*)(void)")]
    public IntPtr padding4;
}