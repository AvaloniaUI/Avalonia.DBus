using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Avalonia.DBus;

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