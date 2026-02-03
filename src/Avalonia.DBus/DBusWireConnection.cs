using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.AutoGen;
using static Avalonia.DBus.AutoGen.LibDbus;
using DBusNativeConnection = Avalonia.DBus.AutoGen.DBusConnection;
using DBusNativeMessage = Avalonia.DBus.AutoGen.DBusMessage;
using DBusWatchPtr = System.IntPtr;
using OtherPtr = System.IntPtr;

namespace Avalonia.DBus.Wire;

/// <summary>
/// Low-level connection handling raw message transport. This is the only IDisposable type in the API.
/// </summary>
public sealed partial class DBusWireConnection : IAsyncDisposable
{
    private static readonly unsafe DBusAddWatchFunction s_addWatchCallback = AddWatchCallback;
    private static readonly unsafe DBusRemoveWatchFunction s_removeWatchCallback = RemoveWatchCallback;
    private static readonly unsafe DBusWatchToggledFunction s_toggleWatchCallback = ToggleWatchCallback;
    private static readonly unsafe DBusWakeupMainFunction s_wakeupCallback = WakeupCallback;
    private static readonly unsafe DBusDispatchStatusFunction s_dispatchCallback = DispatchStatusCallback;
    private static readonly unsafe DBusFreeFunction s_freeCallback = FreeCallback;

    private static readonly unsafe DBusPendingCallNotifyFunction
        s_pendingCallNotifyCallback = PendingCallNotifyCallback;

    private static readonly unsafe DBusHandleMessageFunction s_handleMsgCallback = HandleMessageCallback;

    private static readonly IntPtr s_addWatchPtr = Marshal.GetFunctionPointerForDelegate(s_addWatchCallback);
    private static readonly IntPtr s_removeWatchPtr = Marshal.GetFunctionPointerForDelegate(s_removeWatchCallback);
    private static readonly IntPtr s_toggleWatchPtr = Marshal.GetFunctionPointerForDelegate(s_toggleWatchCallback);
    private static readonly IntPtr s_wakeupPtr = Marshal.GetFunctionPointerForDelegate(s_wakeupCallback);
    private static readonly IntPtr s_dispatchPtr = Marshal.GetFunctionPointerForDelegate(s_dispatchCallback);
    private static readonly IntPtr s_freePtr = Marshal.GetFunctionPointerForDelegate(s_freeCallback);

    private static readonly IntPtr s_pendingCallNotifyPtr =
        Marshal.GetFunctionPointerForDelegate(s_pendingCallNotifyCallback);

    private static readonly IntPtr s_handleMsgPtr = Marshal.GetFunctionPointerForDelegate(s_handleMsgCallback);

    private readonly Channel<DBusMessage> _incoming = Channel.CreateUnbounded<DBusMessage>();
    private readonly TaskCompletionSource _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<DBusWatchPtr, WatchState> _watches = new();
    private unsafe DBusNativeConnection* _connection;
    private bool _disposed;
    private Thread? _workerThread;
    private readonly GCHandle _callbackHandle;

    private unsafe DBusWireConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The D-Bus wire connection requires Linux polling.");
        }

        _connection = connection;
        _closeOnDispose = closeOnDispose;
        _callbackHandle = GCHandle.Alloc(this);

        try
        {
            ConfigureWatchFunctions(connection);
        }
        catch
        {
            _callbackHandle.Free();
            throw;
        }

        var ptr = GCHandle.ToIntPtr(_callbackHandle);
        if (dbus_connection_add_filter(connection, s_handleMsgPtr, (void*)ptr, IntPtr.Zero) != 1)
        {
            _callbackHandle.Free();
            throw new InvalidOperationException("Could not add the message handler to the DBus connection.");
        }

        StartEventLoop();
    }

    private void StartEventLoop()
    {
        _workerThread = new Thread(MainEventLoop)
        {
            IsBackground = true,
            Name = $"DBusWireConnection_{GetHashCode()}"
        };
        _workerThread.Start();
    }

    private const PollEvents PollEventsErrorMask =
        PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL | PollEvents.POLLRDHUP;

    private readonly bool _closeOnDispose;

    private unsafe void MainEventLoop()
    {
        while (!_disposed)
        {
            while (pendingMsgs.TryDequeue(out var pending))
            {
                var replySerial = pending.ReplySerial;
                if (replySerial != 0 && pendingItems.Remove(replySerial, out var replyWorkItem))
                {
                    replyWorkItem.Completion.TrySetResult(pending);
                }
            }
            
            RefreshWatches();

            (IntPtr Key, PollFd pollFd)[] activeWatches =
                _watches.Where(x => x.Value.Enabled)
                    .Select(x => (x.Key, new PollFd
                    {
                        fd = x.Value.Fd,
                        events = x.Value.Events | PollEventsErrorMask,
                        revents = 0
                    }))
                    .ToArray();

            var pollFds = activeWatches.Select(x => x.pollFd).ToArray();

            if (pollFds.Length > 0)
                DoPoll(pollFds);

            for (var i = 0; i < pollFds.Length; i++)
            {
                var handled = dbus_watch_handle(
                    (DBusWatch*)activeWatches[i].Key,
                    ToWatchFlags(pollFds[i].revents));
                Debug.Assert(handled != IntPtr.Zero);
            }

            dbus_connection_ref(_connection);

            while (dbus_connection_dispatch(_connection)
                   == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS) ;

            dbus_connection_unref(_connection);
        }
    }

    private DBusWatchFlags ToWatchFlags(PollEvents revents)
    {
        DBusWatchFlags flags = default;

        if ((revents & PollEvents.POLLIN) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_READABLE;

        if ((revents & PollEvents.POLLIN) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_READABLE;

        if ((revents & PollEvents.POLLIN) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_READABLE;

        if ((revents & PollEvents.POLLOUT) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_WRITABLE;

        if ((revents & PollEvents.POLLHUP) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_HANGUP;

        if ((revents & PollEvents.POLLERR) != 0)
            flags |= DBusWatchFlags.DBUS_WATCH_ERROR;

        return flags;
    }


    private static PollEvents ToPollEvents(DBusWatchFlags watchFlags)
    {
        PollEvents events = PollEvents.None;
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
        LogCalleeVerbose();

        fixed (PollFd* awPtr = &activeFds[0])
            while (true)
            {
                var ret = LinuxPoll.ppoll(awPtr, activeFds.Length, IntPtr.Zero, IntPtr.Zero);
                if (ret >= 0)
                    break;

                var errno = Marshal.GetLastPInvokeError();
                if (errno == LinuxPoll.EINTR)
                    continue;

                throw new InvalidOperationException($"ppoll failed with errno {errno}.");
            }
    }

    private void RefreshWatches()
    {
        LogCalleeVerbose();

        foreach (var watchPtr in _watches)
            RefreshWatch(watchPtr.Key);
    }

    private unsafe void RefreshWatch(DBusWatchPtr watchPtrKey)
    {
        LogCalleeVerbose();

        var watch = (DBusWatch*)watchPtrKey;

        var isEnabled = dbus_watch_get_enabled(watch) != 0;
        var cond = PollEvents.POLLHUP | PollEvents.POLLERR;
        var fd = dbus_watch_get_unix_fd(watch);
        cond |= ToPollEvents(dbus_watch_get_flags(watch));

        _watches[watchPtrKey] = new WatchState(fd, cond, isEnabled);
    }

    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    /// <param name="address">
    /// D-Bus address string (e.g., "unix:path=/run/dbus/system_bus_socket")
    /// or well-known bus type ("session", "system").
    /// </param>
    public static Task<DBusWireConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        LogCalleeVerbose();

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("Address is required.", nameof(address));
        }

        if (string.Equals(address, "session", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectSessionAsync(cancellationToken);
        }

        if (string.Equals(address, "system", StringComparison.OrdinalIgnoreCase))
        {
            return ConnectSystemAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OpenAddress(address));
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static Task<DBusWireConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
    {
        LogCalleeVerbose();

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OpenBus(DBusBusType.DBUS_BUS_SESSION));
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<DBusWireConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OpenBus(DBusBusType.DBUS_BUS_SYSTEM));
    }

    /// <summary>
    /// The unique name assigned by the message bus (e.g., ":1.42").
    /// Null if not connected to a message bus.
    /// </summary>
    public unsafe string? UniqueName
    {
        get
        {
            ThrowIfDisposedOrFailed();
            string? uniqueName = null;
            if (_connection != null)
                uniqueName = DbusHelpers.PtrToStringNullable(dbus_bus_get_unique_name(_connection));
            return uniqueName;
        }
    }

    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    public unsafe Task SendAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        LogCalleeVerbose();

        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposedOrFailed();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var native = DBusMessageMarshaler.ToNative(message);

        try
        {
            uint serial = 0;
            var ret = dbus_connection_send(_connection, native, &serial);
            if (ret == 0)
            {
                tcs.TrySetException(
                    new InvalidOperationException($"Failed to send D-Bus message. libdbus returned {ret}"));
                return tcs.Task;
            }

            if (serial != 0)
            {
                message.Serial = serial;
            }

            tcs.TrySetResult();
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
        finally
        {
            dbus_message_unref(native);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    public async Task<DBusMessage> SendWithReplyAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        LogCalleeVerbose();

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposedOrFailed();

        LogVerbose(
            $"SendWithReply begin: dest='{message.Destination}' path='{message.Path}' iface='{message.Interface}' member='{message.Member}' body={message.Body.Count}");

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        await SendAsync(message, cancellationToken);
        var workItem = new SendWithReplyWorkItem(this, message, tcs, cancellationToken, DateTime.UtcNow);
        var serial = message.Serial;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (!pendingItems.TryRemove(serial, out _))
                    Debug.Assert(false, $"D-Bus connection received an unexpected serial {serial}.");

                tcs.TrySetCanceled(cancellationToken);
            });
        }
        pendingItems[serial] = workItem;
        return await tcs.Task;
    }

    private ConcurrentDictionary<uint, SendWithReplyWorkItem> pendingItems = new();

    /// <summary>
    /// Receives incoming messages (METHOD_CALL, SIGNAL, etc.).
    /// Used for implementing services.
    /// </summary>
    public async IAsyncEnumerable<DBusMessage> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_incoming.Reader is { } reader)
            await foreach (var item in reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
    }

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        LogCalleeVerbose();

        if (_disposed)
        {
            return new ValueTask(_disposeCompletion.Task);
        }

        _disposed = true;
        _incoming.Writer.Complete();
        _disposeCompletion.TrySetResult();
        return new ValueTask(_disposeCompletion.Task);
    }

    private unsafe bool IsActive => !_disposed && _connection != null;

    private unsafe void ThrowIfDisposedOrFailed()
    {
        LogCalleeVerbose();

        if (_disposed || _connection == null)
            throw new ObjectDisposedException(nameof(DBusWireConnection));
    }

    private unsafe void ConfigureWatchFunctions(DBusNativeConnection* connection)
    {
        LogCalleeVerbose();

        var wireInstPtr = (void*)GCHandle.ToIntPtr(_callbackHandle);

        if (dbus_connection_set_watch_functions(connection, s_addWatchPtr, s_removeWatchPtr,
                s_toggleWatchPtr,
                wireInstPtr, IntPtr.Zero) == 0)
        {
            throw new InvalidOperationException("Failed to configure D-Bus watch functions.");
        }
    }

    private unsafe static uint AddWatchCallback(DBusWatch* watch, void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return 0;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
            return 0;

        return wire.AddWatch(watch) ? 1u : 0u;
    }

    private static unsafe void RemoveWatchCallback(DBusWatch* watch, void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection connection)
            return;

        connection.RemoveWatch(watch);
    }

    private static unsafe void ToggleWatchCallback(DBusWatch* watch, void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
            return;

        wire.ToggleWatch(watch);
    }

    private static unsafe void WakeupCallback(void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
            return;

        // wire.WakeupEventLoop();
    }

    private static unsafe void DispatchStatusCallback(DBusNativeConnection* conn, DBusDispatchStatus newStatus,
        void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
            return;

        // if (newStatus == DBusDispatchStatus.DBUS_DISPATCH_COMPLETE)
        //     wire.SleepEventLoop();
        // else if (newStatus == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS)
        //     wire.WakeupEventLoop();
    }


    private static unsafe void FreeCallback(void* data)
    {
        LogCalleeVerbose();

        if (data == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (!handle.IsAllocated || handle.Target is null) return;
        LogVerbose($"Freed {handle.Target.GetHashCode()} - {handle.Target.GetType().Name}");
        handle.Free();
    }


    private static unsafe void PendingCallNotifyCallback(DBusPendingCall* pending, void* userData)
    {
        LogCalleeVerbose();

        if (pending == null || userData == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is not SendWithReplyWorkItem workItem)
            return;

        if (workItem.Connection is not { } wire)
            return;

        wire.PendingCallNotify(pending, workItem);
    }


    private static unsafe DBusHandlerResult HandleMessageCallback(DBusNativeConnection* connection,
        DBusNativeMessage* message,
        void* userData)
    {
        LogCalleeVerbose();

        if (connection == null || message == null || userData == null)
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;

        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (handle.Target is not DBusWireConnection wire)
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;

        return wire.HandleMessage(new IntPtr(message));
    }
    
    private ConcurrentQueue<DBusMessage> pendingMsgs = new ConcurrentQueue<DBusMessage>();

    private DBusHandlerResult HandleMessage(IntPtr message)
    {
        LogCalleeVerbose();

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
                pendingMsgs.Enqueue(msg);
                return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
            }

            _incoming.Writer.TryWrite(msg);
            return DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED;
        }
        catch (Exception e)
        {
            LogVerbose(e.ToString());
        }

        return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
    }

    private unsafe void PendingCallNotify(DBusPendingCall* pending, SendWithReplyWorkItem workItem)
    {
        LogCalleeVerbose();

        try
        {
            var reply = dbus_pending_call_steal_reply(pending);
            if (reply != null)
            {
                var managedReply = DBusMessageMarshaler.FromNative(reply);
                workItem.Completion.TrySetResult(managedReply);
            }
            else
            {
                workItem.Completion.TrySetException(
                    new InvalidOperationException("PendingCallNotify received an empty reply."));
            }
        }
        catch (Exception ex)
        {
            workItem.Completion.TrySetException(ex);
        }
        finally
        {
            dbus_pending_call_unref(pending);
        }
    }

    private unsafe bool AddWatch(DBusWatch* watch)
    {
        LogCalleeVerbose();

        if (watch == null)
            return false;
        if (!TryGetWatchFd(watch, out var fd))
            return false;

        RefreshWatch((IntPtr)watch);
        return true;
    }

    private unsafe void RemoveWatch(DBusWatch* watch)
    {
        LogCalleeVerbose();

        if (watch == null)
            return;

        var watchPtr = (IntPtr)watch;
        _watches.Remove(watchPtr, out _);
    }

    private unsafe void ToggleWatch(DBusWatch* watch)
    {
        LogCalleeVerbose();
        if (watch == null)
            return;

        RefreshWatch((DBusWatchPtr)watch);
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


    private static unsafe void CloseConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        if (connection == null)
        {
            return;
        }

        if (closeOnDispose)
        {
            dbus_connection_close(connection);
        }

        dbus_connection_unref(connection);
    }


    private static unsafe DBusWireConnection OpenBus(DBusBusType busType)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        dbus_error_init(&error);

        var connection = dbus_bus_get(busType, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to connect to D-Bus bus.");
        }

        dbus_connection_set_exit_on_disconnect(connection, 0);
        return new DBusWireConnection(connection, closeOnDispose: false);
    }

    private static unsafe DBusWireConnection OpenAddress(string address)
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
        return new DBusWireConnection(connection, closeOnDispose: true);
    }


    private static void LogVerbose(string message)
    {
#if DEBUG
        Console.Error.WriteLine($"[DBusWire {Environment.CurrentManagedThreadId}] {message}");
#endif
    }


    private static void LogCalleeVerbose([CallerMemberName] string callee = "<nocallee>")
    {
#if DEBUG
        Console.Error.WriteLine($"Callback {callee} called.");
#endif
    }


    private static unsafe void ThrowErrorAndFree(ref DBusError error, string fallbackMessage)
    {
        var name = error.name != null ? DbusHelpers.PtrToString(error.name) : "DBus error";
        var message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;
        LogVerbose($"libdbus error: {name}: {message}");
        fixed (DBusError* errorPtr = &error)
        {
            if (dbus_error_is_set(errorPtr) != 0)
            {
                dbus_error_free(errorPtr);
            }
        }

        throw new InvalidOperationException($"{name}: {message}");
    }
}
