unsafe class WakeupFd
{
    private readonly int _read;
    private readonly int _write;
    private readonly object _lock = new();
    private bool _signaled;

    public int PollFd => _read;

    public WakeupFd()
    {
        int* fds = stackalloc int[2];
        if (pipe2(fds, O_NONBLOCK | O_CLOEXEC) != 0)
            throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
        _read = fds[0];
        _write = fds[1];
    }

    private static readonly void* s_readBuf = (void*)Marshal.AllocHGlobal(1024);
    public void Clear()
    {
        lock (_lock)
        {
            if(!_signaled)
                return;
            var readNow = read(_read, s_readBuf, 1);
            Debug.Assert(readNow <= 1);
            _signaled = false;
        }
    }

    public void Set()
    {
        lock (_lock)
        {
            if(_signaled)
                return;
            byte b = 0;
            write(_write, &b, 1);
            _signaled = true;
        }
    }
}

class WaylandConnection : IDisposable
{
    public WlDisplay Display { get; private set; }
    public WlEventQueue Queue { get; private set; }
    public bool IsConnected { get; private set; } = true;
    private readonly bool _ownsDisplay;
    private readonly int _fd;

    class Listener(WaylandConnection parent) : WlDisplay.Listener
    {
        public event Action<uint, string>? OnError;
        protected override void Error(WlDisplay eventSender, WlProxy objectId, uint code, string message)
        {
            parent.IsConnected = false;
            OnError?.Invoke(code, message);
            base.Error(eventSender, objectId, code, message);
        }
    }
    
    public WaylandConnection(string? path)
    {
        _ownsDisplay = true;
        Display = WlDisplay.Connect(new Listener(this), path);
        Queue = Display.CreateEventQueue();
        _fd = Display.GetFd();
    }
    
    public WaylandConnection(WlDisplay foreignDisplay)
    {
        Display = foreignDisplay;
        Queue = Display.CreateEventQueue();
        _fd = Display.GetFd();
    }

    public void Dispose()
    {
        IsConnected = false;
        Queue.Dispose();
        if (_ownsDisplay)
            Display.Dispose();
    }

        
    public void DispatchQueueOrWakeup(int wakeupFd)
    {
            DispatchQueueOrWakeup(Queue, wakeupFd);
    }


    unsafe void DoPoll(pollfd* fds, int count)
    {
        while (true)
        {
            var pollRet = ppoll(fds, new IntPtr(count), IntPtr.Zero, IntPtr.Zero);
            if (pollRet <= 0)
            {
                var errno = Marshal.GetLastPInvokeError();
                if (errno == (int)Errno.EINTR)
                    continue;
                
                // Release the display read lock
                Display.CancelRead();
                // This is quite likely a non-recoverable error
                throw new AvaloniaWaylandPollException();
            }
            return;
        }
    }

    unsafe PollEvents DoPoll(int fd, PollEvents ev)
    {
        pollfd fds = new()
        {
            fd = fd,
            events = ev
        };
        DoPoll(&fds, 1);
        return fds.revents;
    }

    unsafe bool FlushDisplay(out Errno flushError)
    {
        var fd = Display.GetFd();
        while (true)
        {
            var flushRet = Display.Flush();
            if (flushRet >= 0) // Successful flush
            {
                flushError = default;
                return true;
            }

            flushError = (Errno)Marshal.GetLastPInvokeError();
            
            if (flushError != Errno.EAGAIN) // It's an actual error
                return false;
            
            // Hit network buffer limit, need to wait for socket to be writable
            // Don't attempt wakeups here, we need to send commands to the compositor ASAP
            DoPoll(fd, PollEvents.POLLOUT);
        }
    }
    
    public enum DispatchResult
    {
        Dispatched,
        Wakeup,
        ConnectionReset
    }
    
    
    
    public unsafe DispatchResult DispatchQueueOrWakeup(WlEventQueue queue, int wakeupFd)
    {
        // This initiates the race of sorts for who will have to use the wayland socket
        if (Queue.PrepareRead() == -1)
        {
            // Other thread has won the race and read from the display, we can safely dispatch pending events
            queue.DispatchPending();
            return DispatchResult.Dispatched;
        }
        
        // We won the race and are now responsible to drive libwayland's flush/poll/read cycle
        
        
        // First flush any pending data to the compositor
        if (!FlushDisplay(out var flushError))
        {
            // If somebody wants to read errors they should call Display.Dispatch() 
            Display.CancelRead();
            if (flushError == Errno.EPIPE)
                return DispatchResult.ConnectionReset;
            throw new AvaloniaWaylandFlushException();
        }


        var fds = stackalloc pollfd[2];
        fds[0].fd = Display.GetFd();
        fds[0].events = PollEvents.POLLIN;
        fds[1].fd = wakeupFd;
        fds[1].events = PollEvents.POLLIN;
        DoPoll(fds, 2);

        if (fds[0].revents == default)
        {
            // ppoll got woken up by the wakeup fd
            Display.CancelRead();
            return DispatchResult.Wakeup;
        }

        // Actual read call
        if(Display.ReadEvents() == -1)
        {
            if (Marshal.GetLastPInvokeError() == (int)Errno.EPIPE)
                return DispatchResult.ConnectionReset;
            throw new AvaloniaWaylandReadException();
        }

        Queue.DispatchPending();
        return DispatchResult.Dispatched;
    }
}