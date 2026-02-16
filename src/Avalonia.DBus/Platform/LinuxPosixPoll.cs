using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.Platform;

internal sealed unsafe class LinuxPosixPoll : IPosixPoll
{
    private const int O_NONBLOCK = 0x800;
    private const int O_CLOEXEC = 0x80000;

    public int Eintr => 4;
    public int Eagain => 11;
    public PollEvents PollErrorMask => PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL | PollEvents.POLLRDHUP;

    public int Poll(PollFd* fds, int nfds)
    {
        return ppoll(fds, (IntPtr)nfds, IntPtr.Zero, IntPtr.Zero);
    }

    public int CreatePipe(out int readFd, out int writeFd)
    {
        var fds = stackalloc int[2];
        var result = pipe2(fds, O_NONBLOCK | O_CLOEXEC);
        readFd = fds[0];
        writeFd = fds[1];
        return result;
    }

    public long Read(int fd, void* buf, IntPtr count) => read(fd, buf, count);
    public long Write(int fd, void* buf, IntPtr count) => write(fd, buf, count);
    public int Close(int fd) => close(fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ppoll(PollFd* fds, IntPtr nfds, IntPtr timeout, IntPtr sigmask);

    [DllImport("libc", SetLastError = true)]
    private static extern int pipe2(int* pipefd, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern long read(int fd, void* buf, IntPtr count);

    [DllImport("libc", SetLastError = true)]
    private static extern long write(int fd, void* buf, IntPtr count);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}
