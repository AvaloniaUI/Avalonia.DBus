using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.Platform;

internal sealed unsafe partial class FreeBSDPosixPoll : IPosixPoll
{
    // FreeBSD uses different flag values. pipe2 sets both flags atomically
    // and avoids a variadic fcntl call on ARM64.
    private const int O_NONBLOCK = 0x0004;
    private const int O_CLOEXEC = 0x00100000;

    public int Eintr => 4;
    public int Eagain => 35;
    public PollEvents PollErrorMask => PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL;

    public int Poll(PollFd* fds, int nfds)
    {
        // FreeBSD uses unsigned int for nfds_t on 64-bit platforms as well.
        return poll(fds, (uint)nfds, -1);
    }

    public int CreatePipe(out int readFd, out int writeFd)
    {
        var fds = stackalloc int[2];
        var result = pipe2(fds, O_NONBLOCK | O_CLOEXEC);
        readFd = result == 0 ? fds[0] : -1;
        writeFd = result == 0 ? fds[1] : -1;
        return result;
    }

    public long Read(int fd, void* buf, IntPtr count) => read(fd, buf, count);
    public long Write(int fd, void* buf, IntPtr count) => write(fd, buf, count);
    public int Close(int fd) => close(fd);

    [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static partial int poll(PollFd* fds, uint nfds, int timeout);

    [LibraryImport("libc", EntryPoint = "pipe2", SetLastError = true)]
    private static partial int pipe2(int* pipefd, int flags);

    [LibraryImport("libc", EntryPoint = "read", SetLastError = true)]
    private static partial long read(int fd, void* buf, IntPtr count);

    [LibraryImport("libc", EntryPoint = "write", SetLastError = true)]
    private static partial long write(int fd, void* buf, IntPtr count);

    [LibraryImport("libc", EntryPoint = "close", SetLastError = true)]
    private static partial int close(int fd);
}
