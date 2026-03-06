using System;

namespace Avalonia.DBus;

internal static class DBusTransportLog
{
    public static void UnsupportedUnixFdTransport(IDBusDiagnostics? diagnostics, int fdCount)
        => diagnostics?.Log(
            DBusLogLevel.Warning,
            $"Ignoring {fdCount} D-Bus Unix file descriptor(s) on the managed socket transport " +
            "because .NET sockets do not currently expose sendmsg/recvmsg SCM_RIGHTS APIs.");

    public static void MalformedMessageSkipped(IDBusDiagnostics? diagnostics, Exception exception)
        => diagnostics?.Log(
            DBusLogLevel.Warning,
            $"Skipping malformed D-Bus message: {exception.Message}");

    public static void SocketTransportStopped(IDBusDiagnostics? diagnostics, string direction, Exception exception)
        => diagnostics?.Log(
            DBusLogLevel.Warning,
            $"D-Bus socket transport {direction} stopped: {exception.GetType().Name}: {exception.Message}");

    public static void InboundTransportCompleted(IDBusDiagnostics? diagnostics)
        => diagnostics?.Log(
            DBusLogLevel.Warning,
            "Inbound D-Bus transport completed; the connection will stop receiving messages until it is disposed.");
}
