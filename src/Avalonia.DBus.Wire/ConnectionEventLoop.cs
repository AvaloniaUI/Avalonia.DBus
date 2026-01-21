using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public sealed unsafe class ConnectionEventLoop : IDisposable
{
    private const short PollIn = 0x0001;
    private const short PollOut = 0x0004;
    private const short PollErr = 0x0008;
    private const short PollHup = 0x0010;

    private const int EINTR = 4;
    private const int F_GETFL = 3;
    private const int F_SETFL = 4;
    private const int O_NONBLOCK = 0x800;

    private readonly object _gate = new();
    private readonly Dictionary<IntPtr, WatchState> _watches = new();
    private readonly Dictionary<IntPtr, TimeoutState> _timeouts = new();
    private readonly DBusConnection* _connection;
    private readonly Thread _thread;
    private readonly int _wakeReadFd;
    private readonly int _wakeWriteFd;
    private GCHandle _selfHandle;
    private bool _running;
    private bool _disposed;

    private static readonly DBusAddWatchFunction s_addWatch = AddWatch;
    private static readonly IntPtr s_addWatchPtr = Marshal.GetFunctionPointerForDelegate(s_addWatch);
    private static readonly DBusRemoveWatchFunction s_removeWatch = RemoveWatch;
    private static readonly IntPtr s_removeWatchPtr = Marshal.GetFunctionPointerForDelegate(s_removeWatch);
    private static readonly DBusWatchToggledFunction s_toggleWatch = ToggleWatch;
    private static readonly IntPtr s_toggleWatchPtr = Marshal.GetFunctionPointerForDelegate(s_toggleWatch);

    private static readonly DBusAddTimeoutFunction s_addTimeout = AddTimeout;
    private static readonly IntPtr s_addTimeoutPtr = Marshal.GetFunctionPointerForDelegate(s_addTimeout);
    private static readonly DBusRemoveTimeoutFunction s_removeTimeout = RemoveTimeout;
    private static readonly IntPtr s_removeTimeoutPtr = Marshal.GetFunctionPointerForDelegate(s_removeTimeout);
    private static readonly DBusTimeoutToggledFunction s_toggleTimeout = ToggleTimeout;
    private static readonly IntPtr s_toggleTimeoutPtr = Marshal.GetFunctionPointerForDelegate(s_toggleTimeout);

    private static readonly DBusDispatchStatusFunction s_dispatchStatus = DispatchStatusChanged;
    private static readonly IntPtr s_dispatchStatusPtr = Marshal.GetFunctionPointerForDelegate(s_dispatchStatus);

    public ConnectionEventLoop(DBusConnection* connection)
    {
        if (connection == null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        DbusHelpers.EnsureThreadsInitialized();

        _connection = connection;
        dbus.dbus_connection_ref(_connection);

        try
        {
            CreateWakePipe(out _wakeReadFd, out _wakeWriteFd);

            _selfHandle = GCHandle.Alloc(this);
            try
            {
                if (dbus.dbus_connection_set_watch_functions(_connection, s_addWatchPtr, s_removeWatchPtr, s_toggleWatchPtr, (void*)GCHandle.ToIntPtr(_selfHandle), IntPtr.Zero) == 0)
                {
                    throw new InvalidOperationException("Failed to set D-Bus watch functions.");
                }
                if (dbus.dbus_connection_set_timeout_functions(_connection, s_addTimeoutPtr, s_removeTimeoutPtr, s_toggleTimeoutPtr, (void*)GCHandle.ToIntPtr(_selfHandle), IntPtr.Zero) == 0)
                {
                    throw new InvalidOperationException("Failed to set D-Bus timeout functions.");
                }

                dbus.dbus_connection_set_dispatch_status_function(_connection, s_dispatchStatusPtr, (void*)GCHandle.ToIntPtr(_selfHandle), IntPtr.Zero);
            }
            catch
            {
                dbus.dbus_connection_set_dispatch_status_function(_connection, IntPtr.Zero, null, IntPtr.Zero);
                dbus.dbus_connection_set_watch_functions(_connection, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero);
                dbus.dbus_connection_set_timeout_functions(_connection, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero);
                CleanupHandle();
                CloseWakePipe();
                throw;
            }
        }
        catch
        {
            dbus.dbus_connection_unref(_connection);
            throw;
        }

        _running = true;
        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Name = $"{nameof(Wire)}.EventLoop"
        };
        _thread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _running = false;
        Wake();

        if (_thread.IsAlive)
        {
            _thread.Join(500);
        }

        dbus.dbus_connection_set_dispatch_status_function(_connection, IntPtr.Zero, null, IntPtr.Zero);
        dbus.dbus_connection_set_watch_functions(_connection, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero);
        dbus.dbus_connection_set_timeout_functions(_connection, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, null, IntPtr.Zero);

        CleanupHandle();
        CloseWakePipe();

        dbus.dbus_connection_unref(_connection);
    }

    private static ConnectionEventLoop? TryGetLoop(void* data)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)data);
        return handle.IsAllocated ? handle.Target as ConnectionEventLoop : null;
    }

    private static uint AddWatch(DBusWatch* watch, void* data)
    {
        var loop = TryGetLoop(data);
        if (loop == null)
        {
            return 0;
        }
        loop.AddWatchInternal(watch);
        return 1;
    }

    private static void RemoveWatch(DBusWatch* watch, void* data)
    {
        TryGetLoop(data)?.RemoveWatchInternal(watch);
    }

    private static void ToggleWatch(DBusWatch* watch, void* data)
    {
        TryGetLoop(data)?.ToggleWatchInternal(watch);
    }

    private static uint AddTimeout(DBusTimeout* timeout, void* data)
    {
        var loop = TryGetLoop(data);
        if (loop == null)
        {
            return 0;
        }
        loop.AddTimeoutInternal(timeout);
        return 1;
    }

    private static void RemoveTimeout(DBusTimeout* timeout, void* data)
    {
        TryGetLoop(data)?.RemoveTimeoutInternal(timeout);
    }

    private static void ToggleTimeout(DBusTimeout* timeout, void* data)
    {
        TryGetLoop(data)?.ToggleTimeoutInternal(timeout);
    }

    private static void DispatchStatusChanged(DBusConnection* connection, DBusDispatchStatus newStatus, void* data)
    {
        if (newStatus == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS)
        {
            TryGetLoop(data)?.Wake();
        }
    }

    private void AddWatchInternal(DBusWatch* watch)
    {
        var state = new WatchState(watch);
        lock (_gate)
        {
            _watches[(IntPtr)watch] = state;
        }
        Wake();
    }

    private void RemoveWatchInternal(DBusWatch* watch)
    {
        lock (_gate)
        {
            _watches.Remove((IntPtr)watch);
        }
        Wake();
    }

    private void ToggleWatchInternal(DBusWatch* watch)
    {
        lock (_gate)
        {
            if (_watches.TryGetValue((IntPtr)watch, out var state))
            {
                state.Refresh();
            }
        }
        Wake();
    }

    private void AddTimeoutInternal(DBusTimeout* timeout)
    {
        var state = new TimeoutState(timeout);
        lock (_gate)
        {
            _timeouts[(IntPtr)timeout] = state;
        }
        Wake();
    }

    private void RemoveTimeoutInternal(DBusTimeout* timeout)
    {
        lock (_gate)
        {
            _timeouts.Remove((IntPtr)timeout);
        }
        Wake();
    }

    private void ToggleTimeoutInternal(DBusTimeout* timeout)
    {
        lock (_gate)
        {
            if (_timeouts.TryGetValue((IntPtr)timeout, out var state))
            {
                state.Refresh();
            }
        }
        Wake();
    }

    private void RunLoop()
    {
        while (_running)
        {
            if (DispatchPending())
            {
                continue;
            }

            PollFd[] pollFds;
            WatchState[] pollWatches;
            int timeout = BuildPollList(out pollFds, out pollWatches);

            int pollResult;
            fixed (PollFd* fdsPtr = pollFds)
            {
                pollResult = NativeMethods.poll(fdsPtr, (uint)pollFds.Length, timeout);
            }

            if (!_running)
            {
                break;
            }

            if (pollResult < 0)
            {
                int errno = Marshal.GetLastWin32Error();
                if (errno == EINTR)
                {
                    continue;
                }
            }

            if (pollFds.Length > 0 && pollFds[0].revents != 0)
            {
                DrainWake();
            }

            var readyWatches = CollectReadyWatches(pollFds, pollWatches);
            foreach (var ready in readyWatches)
            {
                dbus.dbus_watch_handle(ready.Watch, ready.Flags);
            }

            HandleDueTimeouts();
            DispatchPending();
        }
    }

    private bool DispatchPending()
    {
        bool dispatched = false;
        while (dbus.dbus_connection_get_dispatch_status(_connection) == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS)
        {
            dispatched = true;
            var status = dbus.dbus_connection_dispatch(_connection);
            if (status == DBusDispatchStatus.DBUS_DISPATCH_NEED_MEMORY)
            {
                Thread.Sleep(1);
                break;
            }
        }
        return dispatched;
    }

    private int BuildPollList(out PollFd[] pollFds, out WatchState[] pollWatches)
    {
        lock (_gate)
        {
            int count = 1;
            foreach (var watch in _watches.Values)
            {
                if (watch.Enabled && watch.Fd >= 0)
                {
                    count++;
                }
            }

            pollFds = new PollFd[count];
            pollWatches = new WatchState[count - 1];
            pollFds[0] = new PollFd
            {
                fd = _wakeReadFd,
                events = PollIn,
                revents = 0
            };

            int index = 1;
            foreach (var watch in _watches.Values)
            {
                if (!watch.Enabled || watch.Fd < 0)
                {
                    continue;
                }

                pollFds[index] = new PollFd
                {
                    fd = watch.Fd,
                    events = watch.PollEvents,
                    revents = 0
                };
                pollWatches[index - 1] = watch;
                index++;
            }

            long now = GetNowMs();
            long? nextDue = null;
            foreach (var timeout in _timeouts.Values)
            {
                if (!timeout.Enabled)
                {
                    continue;
                }

                if (!nextDue.HasValue || timeout.NextDueMs < nextDue.Value)
                {
                    nextDue = timeout.NextDueMs;
                }
            }

            if (!nextDue.HasValue)
            {
                return -1;
            }

            long remaining = nextDue.Value - now;
            if (remaining <= 0)
            {
                return 0;
            }

            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }
    }

    private List<WatchDispatch> CollectReadyWatches(PollFd[] pollFds, WatchState[] pollWatches)
    {
        var result = new List<WatchDispatch>();
        if (pollWatches.Length == 0)
        {
            return result;
        }

        lock (_gate)
        {
            for (int i = 0; i < pollWatches.Length; i++)
            {
                var watch = pollWatches[i];
                if (watch == null)
                {
                    continue;
                }

                short revents = pollFds[i + 1].revents;
                if (revents == 0)
                {
                    continue;
                }

                if (!_watches.ContainsKey((IntPtr)watch.Watch) || !watch.Enabled)
                {
                    continue;
                }

                uint flags = MapPollToDbusFlags(revents);
                if (flags != 0)
                {
                    result.Add(new WatchDispatch(watch.Watch, flags));
                }
            }
        }

        return result;
    }

    private void HandleDueTimeouts()
    {
        List<IntPtr> due = new();
        long now = GetNowMs();
        lock (_gate)
        {
            foreach (var timeout in _timeouts.Values)
            {
                if (!timeout.Enabled)
                {
                    continue;
                }

                if (timeout.NextDueMs <= now)
                {
                    due.Add((IntPtr)timeout.Timeout);
                    timeout.ScheduleNext(now);
                }
            }
        }

        foreach (var timeoutPtr in due)
        {
            dbus.dbus_timeout_handle((DBusTimeout*)timeoutPtr);
        }
    }

    private void Wake()
    {
        if (_wakeWriteFd < 0)
        {
            return;
        }

        byte value = 1;
        NativeMethods.write(_wakeWriteFd, &value, 1);
    }

    private void DrainWake()
    {
        if (_wakeReadFd < 0)
        {
            return;
        }

        byte* buffer = stackalloc byte[64];
        while (true)
        {
            long read = NativeMethods.read(_wakeReadFd, buffer, 64);
            if (read <= 0)
            {
                break;
            }
        }
    }

    private void CloseWakePipe()
    {
        if (_wakeReadFd >= 0)
        {
            NativeMethods.close(_wakeReadFd);
        }
        if (_wakeWriteFd >= 0)
        {
            NativeMethods.close(_wakeWriteFd);
        }
    }

    private void CleanupHandle()
    {
        if (_selfHandle.IsAllocated)
        {
            _selfHandle.Free();
        }
    }

    private static void CreateWakePipe(out int readFd, out int writeFd)
    {
        int* fds = stackalloc int[2];
        if (NativeMethods.pipe(fds) != 0)
        {
            throw new InvalidOperationException("Failed to create wake pipe.");
        }

        readFd = fds[0];
        writeFd = fds[1];
        SetNonBlocking(readFd);
        SetNonBlocking(writeFd);
    }

    private static void SetNonBlocking(int fd)
    {
        int flags = NativeMethods.fcntl(fd, F_GETFL, 0);
        if (flags < 0)
        {
            return;
        }
        NativeMethods.fcntl(fd, F_SETFL, flags | O_NONBLOCK);
    }

    private static uint MapPollToDbusFlags(short revents)
    {
        uint flags = 0;
        if ((revents & PollIn) != 0)
        {
            flags |= (uint)DBusWatchFlags.DBUS_WATCH_READABLE;
        }
        if ((revents & PollOut) != 0)
        {
            flags |= (uint)DBusWatchFlags.DBUS_WATCH_WRITABLE;
        }
        if ((revents & PollErr) != 0)
        {
            flags |= (uint)DBusWatchFlags.DBUS_WATCH_ERROR;
        }
        if ((revents & PollHup) != 0)
        {
            flags |= (uint)DBusWatchFlags.DBUS_WATCH_HANGUP;
        }
        return flags;
    }

    private sealed class WatchState
    {
        public WatchState(DBusWatch* watch)
        {
            Watch = watch;
            Refresh();
        }

        public DBusWatch* Watch { get; }
        public int Fd { get; private set; }
        public uint Flags { get; private set; }
        public bool Enabled { get; private set; }

        public short PollEvents
        {
            get
            {
                short events = 0;
                if ((Flags & (uint)DBusWatchFlags.DBUS_WATCH_READABLE) != 0)
                {
                    events |= PollIn;
                }
                if ((Flags & (uint)DBusWatchFlags.DBUS_WATCH_WRITABLE) != 0)
                {
                    events |= PollOut;
                }
                if ((Flags & (uint)DBusWatchFlags.DBUS_WATCH_ERROR) != 0)
                {
                    events |= PollErr;
                }
                if ((Flags & (uint)DBusWatchFlags.DBUS_WATCH_HANGUP) != 0)
                {
                    events |= PollHup;
                }
                return events;
            }
        }

        public void Refresh()
        {
            int fd = dbus.dbus_watch_get_unix_fd(Watch);
            if (fd < 0)
            {
                fd = dbus.dbus_watch_get_socket(Watch);
            }
            Fd = fd;
            Flags = dbus.dbus_watch_get_flags(Watch);
            Enabled = dbus.dbus_watch_get_enabled(Watch) != 0;
        }
    }

    private sealed class TimeoutState
    {
        public TimeoutState(DBusTimeout* timeout)
        {
            Timeout = timeout;
            Refresh();
        }

        public DBusTimeout* Timeout { get; }
        public int IntervalMs { get; private set; }
        public bool Enabled { get; private set; }
        public long NextDueMs { get; private set; }

        public void Refresh()
        {
            IntervalMs = dbus.dbus_timeout_get_interval(Timeout);
            if (IntervalMs < 0)
            {
                IntervalMs = 0;
            }
            Enabled = dbus.dbus_timeout_get_enabled(Timeout) != 0;
            if (Enabled)
            {
                ScheduleNext(GetNowMs());
            }
        }

        public void ScheduleNext(long now)
        {
            int interval = IntervalMs <= 0 ? 1 : IntervalMs;
            NextDueMs = now + interval;
        }
    }

    private readonly struct WatchDispatch
    {
        public WatchDispatch(DBusWatch* watch, uint flags)
        {
            Watch = watch;
            Flags = flags;
        }

        public DBusWatch* Watch { get; }
        public uint Flags { get; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    private static long GetNowMs()
    {
        return (Stopwatch.GetTimestamp() * 1000) / Stopwatch.Frequency;
    }

    private static class NativeMethods
    {
        [DllImport("libc", SetLastError = true)]
        public static extern int pipe(int* fds);

        [DllImport("libc", SetLastError = true)]
        public static extern int poll(PollFd* fds, uint nfds, int timeout);

        [DllImport("libc", SetLastError = true)]
        public static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        public static extern int fcntl(int fd, int cmd, int arg);

        [DllImport("libc", SetLastError = true)]
        public static extern long read(int fd, void* buffer, ulong count);

        [DllImport("libc", SetLastError = true)]
        public static extern long write(int fd, void* buffer, ulong count);
    }
}
