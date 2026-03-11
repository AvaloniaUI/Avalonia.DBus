using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Native;
using Avalonia.DBus.Platform;
using static Avalonia.DBus.Native.LibDbus;
using DBusNativeConnection = Avalonia.DBus.Native.DBusConnection;
using DBusNativeMessage = Avalonia.DBus.Native.DBusMessage;
using DBusNativeMessagePtr = System.IntPtr;
using DBusWatchPtr = System.IntPtr;

namespace Avalonia.DBus;

internal sealed partial class LibDBusWireWorker
{
    private static readonly unsafe DBusAddWatchFunction AddWatchDelegate = AddWatchCallback;
    private static readonly unsafe DBusRemoveWatchFunction RemoveWatchDelegate = RemoveWatchCallback;
    private static readonly unsafe DBusWatchToggledFunction ToggleWatchDelegate = ToggleWatchCallback;
    private static readonly unsafe DBusHandleMessageFunction HandleMsgDelegate = HandleMessageCallback;

    private static readonly DBusNativeMessagePtr AddWatchPtr = Marshal.GetFunctionPointerForDelegate(AddWatchDelegate);

    private static readonly DBusNativeMessagePtr RemoveWatchPtr =
        Marshal.GetFunctionPointerForDelegate(RemoveWatchDelegate);

    private static readonly DBusNativeMessagePtr ToggleWatchPtr =
        Marshal.GetFunctionPointerForDelegate(ToggleWatchDelegate);

    private static readonly DBusNativeMessagePtr
        HandleMsgPtr = Marshal.GetFunctionPointerForDelegate(HandleMsgDelegate);

    private static readonly ConcurrentDictionary<int, LibDBusWireWorker> ActiveWorkers = new();
    private static int _activeWorkerCounter;

    private readonly unsafe DBusNativeConnection* _connection;

    private readonly Channel<DBusMessage> _receiving = Channel.CreateUnbounded<DBusMessage>();

    private readonly Channel<WireWorkerMessage> _messageQueue = Channel.CreateUnbounded<WireWorkerMessage>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<uint, SendWorkItem> _pendingReplies = new();
    private readonly ConcurrentDictionary<DBusWatchPtr, WatchState> _watches = new();

    private readonly TaskCompletionSource _disposeCompletion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Thread? _workerThread;

    private bool _disposed;
    private int _disposeRequested;
    private readonly int _activeWorkerId;
    private WakeupFd _curWakeupFd = null!;

    private readonly IPosixPoll _poll;
    private readonly bool _closeOnDispose;
    private readonly IDBusDiagnostics? _diagnostics;

    internal ChannelReader<DBusMessage> ReceivingReader => _receiving.Reader;
    internal Task DisposeTask => _disposeCompletion.Task;

    private unsafe LibDBusWireWorker(DBusNativeConnection* connection, bool closeOnDispose, IDBusDiagnostics? diagnostics)
    {
        _poll = PosixPollFactory.Create();
        _curWakeupFd = new WakeupFd(_poll);
        _connection = connection;
        _closeOnDispose = closeOnDispose;
        _diagnostics = diagnostics;
        _activeWorkerId = Interlocked.Increment(ref _activeWorkerCounter);
        ActiveWorkers[_activeWorkerId] = this;

        try
        {
            ConfigureWatchFunctions(connection);
        }
        catch
        {
            ActiveWorkers.TryRemove(_activeWorkerId, out _);
            throw;
        }

        if (dbus_connection_add_filter(connection, HandleMsgPtr,
                (void*)_activeWorkerId, IntPtr.Zero) != 1)
        {
            ActiveWorkers.TryRemove(_activeWorkerId, out _);
            throw new InvalidOperationException("Could not add the message handler to the DBus connection.");
        }

        StartEventLoop();
    }

    internal static unsafe LibDBusWireWorker OpenBus(DBusBusType busType, IDBusDiagnostics? diagnostics = null)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        dbus_error_init(&error);

        var connection = dbus_bus_get_private(busType, &error);
        if (connection == null)
            ThrowErrorAndFree(ref error, "Failed to connect to D-Bus bus.");

        dbus_connection_set_exit_on_disconnect(connection, 0);
        return new LibDBusWireWorker(connection, closeOnDispose: true, diagnostics);
    }

    internal static unsafe LibDBusWireWorker OpenAddress(string address, IDBusDiagnostics? diagnostics = null)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        dbus_error_init(&error);

        using var addressUtf8 = new Utf8String(address);
        var connection = dbus_connection_open_private(addressUtf8.Pointer, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to open D-Bus connection.");
        }

        if (dbus_bus_register(connection, &error) == 0)
        {
            dbus_connection_close(connection);
            dbus_connection_unref(connection);
            ThrowErrorAndFree(ref error, "Failed to register D-Bus connection.");
        }

        dbus_connection_set_exit_on_disconnect(connection, 0);
        return new LibDBusWireWorker(connection, closeOnDispose: true, diagnostics);
    }

    internal bool TryEnqueue(WireWorkerMessage message)
    {
        if (message is DisposeMessage)
        {
            if (Interlocked.Exchange(ref _disposeRequested, 1) != 0)
                return true;
        }
        else if (_disposed || Volatile.Read(ref _disposeRequested) != 0)
        {
            FailMessage(message, new ObjectDisposedException(nameof(LibDBusWireConnection)));
            return false;
        }

        if (!_messageQueue.Writer.TryWrite(message))
        {
            FailMessage(message, new ObjectDisposedException(nameof(LibDBusWireConnection)));
            return false;
        }

        if (!_disposed)
            _curWakeupFd.Set();

        return true;
    }

    private void StartEventLoop()
    {
        _workerThread = new Thread(MainEventLoop)
        {
            IsBackground = true,
            Name = $"LibDbusWireWorker_{GetHashCode()}"
        };
        _workerThread.Start();
    }

    private unsafe void MainEventLoop()
    {
        try
        {
            var pollErrorMask = _poll.PollErrorMask;
            while (!_disposed)
            {
                ProcessQueuedMessages();

                if (_disposed)
                    break;

                RefreshWatches();

                (IntPtr Key, PollFd pollFd)[] activeWatches =
                    _watches.Where(x => x.Value.Enabled)
                        .Select(x => (x.Key, new PollFd
                        {
                            fd = x.Value.Fd,
                            events = x.Value.Events | pollErrorMask,
                            revents = 0
                        }))
                        .ToArray();

                var pollFds1 = activeWatches.Select(x => x.pollFd).ToList();

                pollFds1.Add(new PollFd
                {
                    fd = _curWakeupFd.PollFd,
                    events = pollErrorMask | PollEvents.POLLIN,
                    revents = 0
                });

                var pollFds = pollFds1.ToArray();

                if (pollFds.Length > 0)
                    DoPoll(pollFds);

                for (var i = 0; i < activeWatches.Length; i++)
                {
                    var handled = dbus_watch_handle(
                        (DBusWatch*)activeWatches[i].Key,
                        ToWatchFlags(pollFds[i].revents));

                    Debug.Assert(handled != IntPtr.Zero);
                }

                var wakeupIndex = pollFds.Length - 1;
                if (wakeupIndex >= 0 &&
                    (pollFds[wakeupIndex].revents &
                     (PollEvents.POLLIN | PollEvents.POLLERR | PollEvents.POLLHUP)) != 0)
                    _curWakeupFd.Clear();

                while (dbus_connection_dispatch(_connection)
                       == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS) ;
            }
        }
        catch (Exception ex)
        {
            // Unhandled exceptions on the worker thread would otherwise leave callers hanging forever.
            _diagnostics?.OnUnobservedException(ex);
            try
            {
                DisposeOnWorker();
            }
            catch (Exception disposeEx)
            {
                _diagnostics?.OnUnobservedException(disposeEx);
            }
            finally
            {
                DrainAndFailPendingMessages();
            }
        }
        finally
        {
            _curWakeupFd.Dispose();
        }
    }

    private void ProcessQueuedMessages()
    {
        while (_messageQueue.Reader.TryRead(out var message))
        {
            try
            {
                if (ProcessMessage(message))
                {
                    DrainAndFailPendingMessages();
                    return;
                }
            }
            catch (Exception ex)
            {
                LogVerbose(ex.ToString());
            }
        }
    }

    private bool ProcessMessage(WireWorkerMessage message)
    {
        switch (message)
        {
            case DisposeMessage:
                DisposeOnWorker();
                return true;
            case FetchUniqueNameMessage fetch:
                ProcessFetchUniqueName(fetch);
                return false;
            case EnqueueSendItemMessage send:
                ProcessSendRequest(send);
                return false;
            case CancelSendItemMessage cancel:
                ProcessSendCancellation(cancel);
                return false;
            case EnqueueHandleCallbackMessage handle:
                HandleMessage(handle.MsgPtr);
                return false;
            case AddWatchMessage add:
                AddWatch(add.WatchPtr);
                return false;
            case ToggleWatchMessage toggle:
                ToggleWatch(toggle.WatchPtr);
                return false;
            case RemoveWatchMessage remove:
                RemoveWatch(remove.WatchPtr);
                return false;
            default:
                return false;
        }
    }

    private void DrainAndFailPendingMessages()
    {
        while (_messageQueue.Reader.TryRead(out var message))
        {
            FailMessage(message, new ObjectDisposedException(nameof(LibDBusWireConnection)));
        }
    }

    private void FailMessage(WireWorkerMessage message, Exception exception)
    {
        switch (message)
        {
            case FetchUniqueNameMessage fetch:
                fetch.ReturnTcs.TrySetException(exception);
                break;
            case EnqueueSendItemMessage send:
                send.Completion.TrySetException(exception);
                break;
            case EnqueueHandleCallbackMessage handle:
                ReleaseNativeMessage(handle.MsgPtr);
                break;
        }
    }

    private unsafe void ReleaseNativeMessage(DBusNativeMessagePtr messagePtr)
    {
        if (messagePtr == IntPtr.Zero)
            return;

        dbus_message_unref((DBusNativeMessage*)messagePtr);
    }

    private void ProcessFetchUniqueName(FetchUniqueNameMessage fetch)
    {
        if (_disposed || Volatile.Read(ref _disposeRequested) != 0)
        {
            fetch.ReturnTcs.TrySetException(new ObjectDisposedException(nameof(LibDBusWireConnection)));
            return;
        }

        try
        {
            fetch.ReturnTcs.TrySetResult(GetUniqueNameCore());
        }
        catch (Exception ex)
        {
            fetch.ReturnTcs.TrySetException(ex);
        }
    }

    private void ProcessSendRequest(EnqueueSendItemMessage send)
    {
        if (_disposed || Volatile.Read(ref _disposeRequested) != 0)
        {
            send.Completion.TrySetException(new ObjectDisposedException(nameof(LibDBusWireConnection)));
            return;
        }

        if (send.ExpectingReply)
        {
            LogVerbose(
                $"SendWithReply begin: dest='{send.Message.Destination}' " +
                $"path='{send.Message.Path}' iface='{send.Message.Interface}' " +
                $"member='{send.Message.Member}' body={send.Message.Body.Count}");
        }
        else
        {
            LogVerbose(
                $"Send begin: dest='{send.Message.Destination}' " +
                $"path='{send.Message.Path}' iface='{send.Message.Interface}' " +
                $"member='{send.Message.Member}' body={send.Message.Body.Count}");
        }

        var workItem = new SendWorkItem(
            send.Message,
            send.Completion,
            DateTime.UtcNow,
            send.ExpectingReply,
            send.CancellationToken);

        RegisterCancellation(workItem);
        ProcessSend(workItem);
    }

    private void ProcessSendCancellation(CancelSendItemMessage cancel)
    {
        var serial = cancel.WorkItem.Serial;
        if (serial != 0)
            _pendingReplies.TryRemove(serial, out _);
    }

    private void RegisterCancellation(SendWorkItem workItem)
    {
        if (!workItem.CancellationToken.CanBeCanceled)
            return;

        workItem.CancellationToken.Register(static state =>
        {
            if (state is not CancelRegistrationState cancelState)
                return;

            if (!cancelState.WorkItem.TryCancel())
                return;

            cancelState.Worker.TryEnqueue(new CancelSendItemMessage(cancelState.WorkItem));
        }, new CancelRegistrationState(this, workItem));
    }

    private unsafe void ProcessSend(SendWorkItem pending)
    {
        if (pending.IsCanceled)
        {
            pending.Completion.TrySetCanceled(pending.CancellationToken);
            return;
        }

        DBusNativeMessage* native = null;
        try
        {
            native = DBusMessageMarshaler.ToNative(pending.Message);
            uint serial = 0;
            var ret = dbus_connection_send(_connection, native, &serial);
            if (ret == 0)
                throw new InvalidOperationException($"Failed to send D-Bus message. libdbus returned {ret}");

            if (serial != 0)
                pending.Message.Serial = serial;

            pending.Serial = serial;

            if (pending.ExpectingReply)
            {
                if (serial == 0)
                    throw new InvalidOperationException("Failed to obtain a reply serial for the sent message.");

                if (pending.IsCanceled)
                {
                    pending.Completion.TrySetCanceled(pending.CancellationToken);
                    return;
                }

                _pendingReplies[serial] = pending;
            }
            else
            {
                pending.Completion.TrySetResult(pending.Message);
            }
        }
        catch (Exception ex)
        {
            pending.Completion.TrySetException(ex);
        }
        finally
        {
            if (native != null)
            {
                dbus_message_unref(native);
            }
        }
    }

    private DBusWatchFlags ToWatchFlags(PollEvents revents)
    {
        DBusWatchFlags flags = default;

        if ((revents & (PollEvents.POLLIN | PollEvents.POLLPRI)) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_READABLE;

        if ((revents & PollEvents.POLLOUT) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_WRITABLE;

        if ((revents & (PollEvents.POLLHUP | PollEvents.POLLRDHUP)) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_HANGUP;

        if ((revents & (PollEvents.POLLERR | PollEvents.POLLNVAL)) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_ERROR;

        return flags;
    }

    private static PollEvents ToPollEvents(DBusWatchFlags watchFlags)
    {
        var events = PollEvents.None;
        if ((watchFlags & DBusWatchFlags.DBUS_WATCH_READABLE) != 0)
        {
            events |= PollEvents.POLLIN;
        }

        if ((watchFlags & DBusWatchFlags.DBUS_WATCH_WRITABLE) != 0)
        {
            events |= PollEvents.POLLOUT;
        }

        return events;
    }

    private unsafe void DoPoll(PollFd[] activeFds)
    {
        fixed (PollFd* awPtr = &activeFds[0])
            while (true)
            {
                var ret = _poll.Poll(awPtr, activeFds.Length);
                if (ret >= 0)
                    break;

                var errno = Marshal.GetLastPInvokeError();
                if (errno == _poll.Eintr)
                    continue;

                throw new InvalidOperationException($"poll failed with errno {errno}.");
            }
    }

    private void RefreshWatches()
    {
        foreach (var watchPtr in _watches)
            RefreshWatch(watchPtr.Key);
    }

    private unsafe void RefreshWatch(DBusWatchPtr watchPtrKey)
    {
        var watch = (DBusWatch*)watchPtrKey;

        var isEnabled = dbus_watch_get_enabled(watch) != 0;
        var cond = PollEvents.POLLHUP | PollEvents.POLLERR;
        var fd = dbus_watch_get_unix_fd(watch);
        cond |= ToPollEvents(dbus_watch_get_flags(watch));

        _watches[watchPtrKey] = new WatchState(fd, cond, isEnabled);
    }

    private unsafe void ConfigureWatchFunctions(DBusNativeConnection* connection)
    {
        if (dbus_connection_set_watch_functions(connection, AddWatchPtr, RemoveWatchPtr,
                ToggleWatchPtr,
                (void*)_activeWorkerId, IntPtr.Zero) == 0)
        {
            throw new InvalidOperationException("Failed to configure D-Bus watch functions.");
        }
    }

    private static void LogNativeCallbackException(Exception ex, int workerId)
    {
        if (ActiveWorkers.TryGetValue(workerId, out var worker))
            worker._diagnostics?.OnUnobservedException(ex);
    }

    private static unsafe uint AddWatchCallback(DBusWatch* watch, void* data)
    {
        var workerId = data != null ? (int)data : 0;
        try
        {
            if (data == null || !ActiveWorkers.TryGetValue(workerId, out var wire))
                return 0;

            if (watch == null)
                return 0;

            if (!TryGetWatchFd(watch, out _))
                return 0;

            return wire.TryEnqueue(new AddWatchMessage((IntPtr)watch)) ? 1u : 0u;
        }
        catch (Exception ex)
        {
            LogNativeCallbackException(ex, workerId);
            return 0;
        }
    }

    private static unsafe void RemoveWatchCallback(DBusWatch* watch, void* data)
    {
        var workerId = data != null ? (int)data : 0;
        try
        {
            if (data == null || !ActiveWorkers.TryGetValue(workerId, out var wire))
                return;

            if (watch == null)
                return;

            wire.TryEnqueue(new RemoveWatchMessage((IntPtr)watch));
        }
        catch (Exception ex)
        {
            LogNativeCallbackException(ex, workerId);
        }
    }

    private static unsafe void ToggleWatchCallback(DBusWatch* watch, void* data)
    {
        var workerId = data != null ? (int)data : 0;
        try
        {
            if (data == null || !ActiveWorkers.TryGetValue(workerId, out var wire))
                return;

            if (watch == null)
                return;

            wire.TryEnqueue(new ToggleWatchMessage((IntPtr)watch));
        }
        catch (Exception ex)
        {
            LogNativeCallbackException(ex, workerId);
        }
    }

    private static unsafe DBusHandlerResult HandleMessageCallback(DBusNativeConnection* connection,
        DBusNativeMessage* message,
        void* userData)
    {
        var workerId = userData != null ? (int)userData : 0;
        try
        {
            if (connection == null || message == null || userData == null ||
                !ActiveWorkers.TryGetValue(workerId, out var worker))
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;

            var type = (DBusMessageType)dbus_message_get_type(message);
            var isReply = type is DBusMessageType.MethodReturn or DBusMessageType.Error;

            dbus_message_ref(message);
            var enqueued = worker.TryEnqueue(new EnqueueHandleCallbackMessage((IntPtr)message));

            return enqueued && !isReply
                ? DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED
                : DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }
        catch (Exception ex)
        {
            LogNativeCallbackException(ex, workerId);
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }
    }

    private void HandleMessage(DBusNativeMessagePtr message)
    {
        try
        {
            DBusMessage? msg;
            unsafe
            {
                var msg1 = (DBusNativeMessage*)message.ToPointer();
                msg = DBusMessageMarshaler.FromNative(msg1);
            }

            if (msg.Type is DBusMessageType.MethodReturn or DBusMessageType.Error)
            {
                var replySerial = msg.ReplySerial;

                if (replySerial != 0 && _pendingReplies.TryRemove(replySerial, out var pending))
                    pending.Completion.TrySetResult(msg);

                return;
            }

            _receiving.Writer.TryWrite(msg);
        }
        catch (Exception e)
        {
            LogVerbose(e.ToString());
        }
        finally
        {
            ReleaseNativeMessage(message);
        }
    }

    private unsafe string? GetUniqueNameCore()
    {
        string? uniqueName = null;
        if (_connection != null)
            uniqueName = DbusHelpers.PtrToStringNullable(dbus_bus_get_unique_name(_connection));

        return uniqueName;
    }

    private unsafe void DisposeOnWorker()
    {
        if (_disposed)
        {
            _disposeCompletion.TrySetResult();
            return;
        }

        _disposed = true;
        _receiving.Writer.TryComplete();
        _messageQueue.Writer.TryComplete();
        _watches.Clear();

        foreach (var kvp in _pendingReplies)
        {
            kvp.Value.Fail(new ObjectDisposedException(nameof(LibDBusWireConnection)));
        }

        _pendingReplies.Clear();

        // Prevent accumulating filters on shared bus connections (dbus_bus_get).
        try
        {
            dbus_connection_remove_filter(_connection, HandleMsgPtr, (void*)_activeWorkerId);
        }
        catch (Exception ex)
        {
            LogVerbose(ex.ToString());
        }

        if (_closeOnDispose)
        {
            dbus_connection_close(_connection);
        }

        dbus_connection_unref(_connection);
        ActiveWorkers.TryRemove(_activeWorkerId, out _);
        _disposeCompletion.TrySetResult();
    }

    private unsafe bool AddWatch(DBusWatchPtr watchPtr)
    {
        if (watchPtr == IntPtr.Zero)
            return false;

        var watch = (DBusWatch*)watchPtr;
        if (!TryGetWatchFd(watch, out _))
            return false;

        RefreshWatch(watchPtr);
        return true;
    }

    private void RemoveWatch(DBusWatchPtr watchPtr)
    {
        if (watchPtr == IntPtr.Zero)
            return;

        _watches.Remove(watchPtr, out _);
    }

    private void ToggleWatch(DBusWatchPtr watchPtr)
    {
        if (watchPtr == IntPtr.Zero)
            return;

        RefreshWatch(watchPtr);
    }

    private static unsafe bool TryGetWatchFd(DBusWatch* watch, out int fd)
    {
        var localFd = dbus_watch_get_unix_fd(watch);
        if (localFd >= 0)
        {
            fd = localFd;
            return true;
        }

        fd = -1;
        return false;
    }

    private void LogVerbose(string message)
    {
        _diagnostics?.Log(DBusLogLevel.Verbose, message);
    }

    private static unsafe void ThrowErrorAndFree(ref DBusError error, string fallbackMessage)
    {
        var name = error.name != null ? DbusHelpers.PtrToString(error.name) : "org.freedesktop.DBus.Error.Failed";
        var message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;

        fixed (DBusError* errorPtr = &error)
            if (dbus_error_is_set(errorPtr) != 0)
                dbus_error_free(errorPtr);

        throw new DBusException(name, message);
    }
}
