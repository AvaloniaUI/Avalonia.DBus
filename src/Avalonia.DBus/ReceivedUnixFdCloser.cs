using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Avalonia.DBus;

/// <summary>
/// Closes Unix file descriptors carried by a received D-Bus message.
/// </summary> 
internal static partial class ReceivedUnixFdCloser
{
    public static void CloseAll(IReadOnlyList<object> body)
    {
        foreach (var item in body)
        {
            CloseRecursive(item);
        }
    }

    private static void CloseRecursive(object? value)
    {
        switch (value)
        {
            case null:
            case string:
                return;
            case DBusUnixFd fd:
                Close(fd.Fd);
                return;
            case DBusVariant variant:
                CloseRecursive(variant.Value);
                return;
            case KeyValuePair<object?, object?> pair:
                CloseRecursive(pair.Key);
                CloseRecursive(pair.Value);
                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    CloseRecursive(entry.Key);
                    CloseRecursive(entry.Value);
                }

                return;
            case IEnumerable sequence:
                foreach (var item in sequence)
                {
                    CloseRecursive(item);
                }

                return;
        }
    }

    private static void Close(int fd)
    {
        if (fd < 0)
        {
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            CloseDarwin(fd);
        }
        else
        {
            CloseLibc(fd);
        }
    }

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int CloseLibc(int fd);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "close", SetLastError = true)]
    private static partial int CloseDarwin(int fd);
}
