using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Avalonia.DBus.Wire;

[Flags]
internal enum PollEvents : short
{
    None = 0,
    POLLIN = 0x0001,
    POLLPRI = 0x0002,
    POLLOUT = 0x0004,
    POLLERR = 0x0008,
    POLLHUP = 0x0010,
    POLLNVAL = 0x0020,
    POLLRDHUP = 0x2000
}

[StructLayout(LayoutKind.Sequential)]
internal struct PollFd
{
    public int fd;
    public PollEvents events;
    public PollEvents revents;
}

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

internal sealed unsafe class WakeupFd : IDisposable
{
    private readonly int _read;
    private readonly int _write;
    private readonly object _lock = new();
    private bool _signaled;
    private static readonly void* s_readBuf = (void*)Marshal.AllocHGlobal(8);

    public int PollFd => _read;

    public WakeupFd()
    {
        int* fds = stackalloc int[2];
        if (LinuxPoll.pipe2(fds, LinuxPoll.O_NONBLOCK | LinuxPoll.O_CLOEXEC) != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        _read = fds[0];
        _write = fds[1];
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (!_signaled)
            {
                return;
            }

            while (true)
            {
                var readNow = LinuxPoll.read(_read, s_readBuf, (IntPtr)1);
                if (readNow > 0)
                {
                    continue;
                }

                if (readNow == 0)
                {
                    break;
                }

                int errno = Marshal.GetLastPInvokeError();
                if (errno == LinuxPoll.EINTR)
                {
                    continue;
                }

                if (errno == LinuxPoll.EAGAIN)
                {
                    break;
                }

                break;
            }

            _signaled = false;
        }
    }

    public void Set()
    {
        lock (_lock)
        {
            if (_signaled)
            {
                return;
            }

            byte b = 0;
            LinuxPoll.write(_write, &b, (IntPtr)1);
            _signaled = true;
        }
    }

    public void Dispose()
    {
        LinuxPoll.close(_read);
        LinuxPoll.close(_write);
    }
}
