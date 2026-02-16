using System;

namespace Avalonia.DBus.Platform;

internal static class PosixPollFactory
{
    internal static IPosixPoll Create()
    {
        if (OperatingSystem.IsLinux()) return new LinuxPosixPoll();
        if (OperatingSystem.IsMacOS()) return new MacOSPosixPoll();
        throw new PlatformNotSupportedException("D-Bus requires Linux or macOS.");
    }
}
