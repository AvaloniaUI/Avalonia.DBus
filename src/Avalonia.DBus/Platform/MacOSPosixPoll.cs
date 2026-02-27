using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.Platform;

internal sealed unsafe partial class MacOSPosixPoll : IPosixPoll
{
    public int Eintr => 4;
    public int Eagain => 35;
    public PollEvents PollErrorMask => PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL;

    public int Poll(PollFd* fds, int nfds)
    {
        return poll(fds, (uint)nfds, -1);
    }

    public int CreatePipe(out int readFd, out int writeFd)
    {
        var fds = stackalloc int[2];
        var result = pipe(fds);
        if (result != 0)
        {
            readFd = -1;
            writeFd = -1;
            return result;
        }

        readFd = fds[0];
        writeFd = fds[1];
        // Note: fcntl is variadic and cannot be called via P/Invoke on ARM64 macOS
        // (dotnet/runtime#48752). We skip O_NONBLOCK and FD_CLOEXEC here;
        // WakeupFd handles both blocking and non-blocking pipes correctly,
        // and FD_CLOEXEC is non-critical for an internal wakeup pipe.
        return 0;
    }

    public long Read(int fd, void* buf, IntPtr count) => read(fd, buf, count);
    public long Write(int fd, void* buf, IntPtr count) => write(fd, buf, count);
    public int Close(int fd) => close(fd);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "poll", SetLastError = true)]
    private static partial int poll(PollFd* fds, uint nfds, int timeout);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "pipe", SetLastError = true)]
    private static partial int pipe(int* pipefd);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "read", SetLastError = true)]
    private static partial long read(int fd, void* buf, IntPtr count);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "write", SetLastError = true)]
    private static partial long write(int fd, void* buf, IntPtr count);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "close", SetLastError = true)]
    private static partial int close(int fd);
}
