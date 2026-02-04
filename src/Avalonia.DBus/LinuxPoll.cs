using System;
using System.Runtime.InteropServices;

namespace Avalonia.DBus;

internal static unsafe class LinuxPoll
{
    internal const int O_NONBLOCK = 0x800;
    internal const int O_CLOEXEC = 0x80000;
    internal const int EINTR = 4;
    internal const int EAGAIN = 11;

    [DllImport("libc", SetLastError = true)]
    internal static extern int ppoll(PollFd* fds, IntPtr nfds, IntPtr timeout, IntPtr sigmask);

    [DllImport("libc", SetLastError = true)]
    internal static extern int pipe2(int* pipefd, int flags);

    [DllImport("libc", SetLastError = true)]
    internal static extern long read(int fd, void* buf, IntPtr count);

    [DllImport("libc", SetLastError = true)]
    internal static extern long write(int fd, void* buf, IntPtr count);

    [DllImport("libc", SetLastError = true)]
    internal static extern int close(int fd);
}