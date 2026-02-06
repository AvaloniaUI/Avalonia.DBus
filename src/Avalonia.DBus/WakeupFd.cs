using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Avalonia.DBus.LinuxPoll;

namespace Avalonia.DBus;

internal sealed unsafe class WakeupFd : IDisposable
{
    private readonly int _write;
    private readonly object _lock = new();
    private bool _signaled;
    private static readonly void* s_readBuf = (void*)Marshal.AllocHGlobal(8);

    public int PollFd { get; }

    public WakeupFd()
    {
        var fds = stackalloc int[2];
        if (pipe2(fds, O_NONBLOCK | O_CLOEXEC) != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        PollFd = fds[0];
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
                var readNow = read(PollFd, s_readBuf, 1);
                if (readNow > 0)
                {
                    continue;
                }

                if (readNow == 0)
                {
                    break;
                }

                var errno = Marshal.GetLastPInvokeError();
                if (errno == EINTR)
                {
                    continue;
                }

                if (errno == EAGAIN)
                {
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
            while (true)
            {
                var written = write(_write, &b, 1);
                if (written == 1)
                {
                    _signaled = true;
                    return;
                }

                var errno = Marshal.GetLastPInvokeError();
                switch (errno)
                {
                    case EINTR:
                        continue;
                    case EAGAIN:  
                        // Non-blocking pipe with a single-byte signal; EAGAIN should only happen if the pipe is already signaled.
                        _signaled = true;
                        return;
                    default:
                        throw new Win32Exception(errno);
                }
            }
        }
    }

    public void Dispose()
    {
        close(PollFd);
        close(_write);
    }
}
