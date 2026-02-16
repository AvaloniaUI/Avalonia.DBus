using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.DBus.Platform;

namespace Avalonia.DBus;

internal sealed unsafe class WakeupFd : IDisposable
{
    private readonly IPosixPoll _poll;
    private readonly int _write;
    private readonly object _lock = new();
    private bool _signaled;
    private static readonly void* s_readBuf = (void*)Marshal.AllocHGlobal(8);

    public int PollFd { get; }

    public WakeupFd(IPosixPoll poll)
    {
        _poll = poll;
        if (_poll.CreatePipe(out var readFd, out var writeFd) != 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        PollFd = readFd;
        _write = writeFd;
    }

    public void Clear()
    {
        lock (_lock)
        {
            if (!_signaled)
            {
                return;
            }

            // Set() only writes a single byte (guarded by _signaled), so we only
            // need to read that one byte.  This works regardless of whether the
            // pipe is non-blocking (Linux pipe2) or blocking (macOS pipe).
            while (true)
            {
                var readNow = _poll.Read(PollFd, s_readBuf, 1);
                if (readNow > 0)
                {
                    break;
                }

                if (readNow == 0)
                {
                    break;
                }

                var errno = Marshal.GetLastPInvokeError();
                if (errno == _poll.Eintr)
                {
                    continue;
                }

                // EAGAIN on non-blocking pipes means no data left — fine.
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
                var written = _poll.Write(_write, &b, 1);
                if (written == 1)
                {
                    _signaled = true;
                    return;
                }

                var errno = Marshal.GetLastPInvokeError();
                if (errno == _poll.Eintr)
                    continue;
                
                if (errno == _poll.Eagain)
                {
                    // Non-blocking pipe with a single-byte signal; EAGAIN should only happen if the pipe is already signaled.
                    _signaled = true;
                    return;
                }

                throw new Win32Exception(errno);
            }
        }
    }

    public void Dispose()
    {
        _poll.Close(PollFd);
        _poll.Close(_write);
    }
}
