using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.AutoGen;
using DBusNativeConnection = Avalonia.DBus.AutoGen.DBusConnection;
using DBusNativeMessage = Avalonia.DBus.AutoGen.DBusMessage;

namespace Avalonia.DBus.Wire;

/// <summary>
/// Low-level connection handling raw message transport. This is the only IDisposable type in the API.
/// </summary>
public sealed unsafe class DBusWireConnection : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly object _pendingGate = new();
    private readonly object _watchGate = new();
    private readonly Dictionary<uint, TaskCompletionSource<DBusMessage>> _pendingReplies = new();
    private static readonly bool s_verbose = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE"));
    private static readonly SharedWorker s_worker = new();
    private static readonly DBusAddWatchFunction s_addWatchCallback = AddWatchCallback;
    private static readonly DBusRemoveWatchFunction s_removeWatchCallback = RemoveWatchCallback;
    private static readonly DBusWatchToggledFunction s_toggleWatchCallback = ToggleWatchCallback;
    private static readonly DBusWakeupMainFunction s_wakeupCallback = WakeupCallback;
    private static readonly DBusDispatchStatusFunction s_dispatchCallback = DispatchStatusCallback;
    private static readonly IntPtr s_addWatchPtr = Marshal.GetFunctionPointerForDelegate(s_addWatchCallback);
    private static readonly IntPtr s_removeWatchPtr = Marshal.GetFunctionPointerForDelegate(s_removeWatchCallback);
    private static readonly IntPtr s_toggleWatchPtr = Marshal.GetFunctionPointerForDelegate(s_toggleWatchCallback);
    private static readonly IntPtr s_wakeupPtr = Marshal.GetFunctionPointerForDelegate(s_wakeupCallback);
    private static readonly IntPtr s_dispatchPtr = Marshal.GetFunctionPointerForDelegate(s_dispatchCallback);
    private readonly AsyncMessageQueue _incoming = new();
    private readonly Channel<SendWorkItem> _sendQueue;
    private readonly TaskCompletionSource _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dictionary<IntPtr, WatchState> _watches = new();
    private GCHandle _watchHandle;
    private bool _watchHandleAllocated;
    private bool _watchConfigured;
    private string? _uniqueName;
    private DBusNativeConnection* _connection;
    private readonly bool _closeOnDispose;
    private bool _disposed;
    private Exception? _workerFailure;

    private DBusWireConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The D-Bus wire connection requires Linux polling.");
        }

        _connection = connection;
        _closeOnDispose = closeOnDispose;
        _uniqueName = DbusHelpers.PtrToStringNullable(LibDbus.dbus_bus_get_unique_name(connection));

        _sendQueue = Channel.CreateUnbounded<SendWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = false
        });

        _watchHandle = GCHandle.Alloc(this);
        _watchHandleAllocated = true;
        try
        {
            ConfigureWatchFunctions(connection);
        }
        catch
        {
            FreeWatchHandle();
            throw;
        }
        s_worker.Register(this);
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
    public string? UniqueName
    {
        get
        {
            ThrowIfDisposedOrFailed();
            lock (_gate)
            {
                if (_connection == null)
                {
                    throw new ObjectDisposedException(nameof(DBusWireConnection));
                }

                var name = DbusHelpers.PtrToStringNullable(LibDbus.dbus_bus_get_unique_name(_connection));
                if (!string.IsNullOrEmpty(name))
                {
                    _uniqueName = name;
                }

                return _uniqueName;
            }
        }
    }

    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    public ValueTask SendAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposedOrFailed();

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new FireAndForgetWorkItem(message, tcs, cancellationToken);
        if (!_sendQueue.Writer.TryWrite(workItem))
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(DBusWireConnection)));
        }
        else
        {
            RequestSendDrain();
        }

        return new ValueTask(tcs.Task);
    }

    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    public Task<DBusMessage> SendWithReplyAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposedOrFailed();
        LogVerbose($"SendWithReply begin: dest='{message.Destination}' path='{message.Path}' iface='{message.Interface}' member='{message.Member}' body={message.Body.Count}");

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workItem = new SendWithReplyWorkItem(message, tcs, cancellationToken, DateTime.UtcNow);
        if (!_sendQueue.Writer.TryWrite(workItem))
        {
            tcs.TrySetException(new ObjectDisposedException(nameof(DBusWireConnection)));
        }
        else
        {
            RequestSendDrain();
        }

        return tcs.Task;
    }

    /// <summary>
    /// Receives incoming messages (METHOD_CALL, SIGNAL, etc.).
    /// Used for implementing services.
    /// </summary>
    public IAsyncEnumerable<DBusMessage> ReceiveAsync(
        CancellationToken cancellationToken = default)
        => _incoming.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        DBusNativeConnection* connectionPtr;
        lock (_gate)
        {
            if (_disposed)
            {
                return new ValueTask(_disposeCompletion.Task);
            }
            _disposed = true;
            connectionPtr = _connection;
            _connection = null;
        }

        _sendQueue.Writer.TryComplete();
        s_worker.ScheduleDispose(this, connectionPtr, _closeOnDispose, _disposeCompletion);
        return new ValueTask(_disposeCompletion.Task);
    }

    private bool DrainSendQueue(DBusNativeConnection* connection, int maxItems)
    {
        var processed = false;
        for (int i = 0; i < maxItems && _sendQueue.Reader.TryRead(out var workItem); i++)
        {
            processed = true;
            if (workItem.CancellationToken.IsCancellationRequested)
            {
                workItem.Cancel();
                continue;
            }

            if (workItem is FireAndForgetWorkItem send)
            {
                ProcessSend(send, connection);
            }
            else if (workItem is SendWithReplyWorkItem sendWithReply)
            {
                ProcessSendWithReply(sendWithReply, connection);
            }
        }

        return processed;
    }

    private void ProcessSend(FireAndForgetWorkItem workItem, DBusNativeConnection* connection)
    {
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(workItem.Message);
        try
        {
            LogVerbose("Send sending...");
            uint serial = 0;
            if (LibDbus.dbus_connection_send(connection, native, &serial) == 0)
            {
                workItem.Fail(new InvalidOperationException("Failed to send D-Bus message."));
                return;
            }

            if (serial != 0)
            {
                workItem.Message.Serial = serial;
            }

            workItem.Complete();
        }
        catch (Exception ex)
        {
            workItem.Fail(ex);
        }
        finally
        {
            LibDbus.dbus_message_unref(native);
        }
    }

    private void ProcessSendWithReply(SendWithReplyWorkItem workItem, DBusNativeConnection* connection)
    {
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(workItem.Message);
        uint serial = 0;
        var pendingAdded = false;
        try
        {
            LogVerbose("SendWithReply sending...");
            if (LibDbus.dbus_connection_send(connection, native, &serial) == 0)
            {
                LogVerbose("SendWithReply send failed (dbus_connection_send returned 0)");
                workItem.Fail(new InvalidOperationException("Failed to send D-Bus message."));
                return;
            }

            if (serial != 0)
            {
                workItem.Message.Serial = serial;
            }
            LogVerbose($"SendWithReply send ok: serial={serial}");

            lock (_pendingGate)
            {
                _pendingReplies[serial] = workItem.Completion;
            }
            pendingAdded = true;

            if (workItem.CancellationToken.CanBeCanceled)
            {
                _ = workItem.CancellationToken.Register(state =>
                {
                    var tuple = (Tuple<DBusWireConnection, uint>)state!;
                    if (tuple.Item1.TryRemovePending(tuple.Item2, out var pending))
                    {
                        pending.TrySetCanceled();
                    }
                }, Tuple.Create(this, serial));
            }

            var elapsed = DateTime.UtcNow - workItem.StartTimestamp;
            LogVerbose($"SendWithReply queued: serial={serial} after {elapsed.TotalMilliseconds:0} ms");
        }
        catch (Exception ex)
        {
            LogVerbose($"SendWithReply failed: {ex.GetType().Name}: {ex.Message}");
            if (pendingAdded)
            {
                TryRemovePending(serial, out _);
            }
            workItem.Fail(ex);
        }
        finally
        {
            LibDbus.dbus_message_unref(native);
        }
    }

    private void RequestSendDrain()
        => s_worker.Wakeup();

    private bool WorkerDrainSendQueue(int maxItems)
    {
        var connection = _connection;
        if (connection == null || _disposed || _workerFailure != null)
        {
            return false;
        }

        try
        {
            if (LibDbus.dbus_connection_has_messages_to_send(connection) != 0)
            {
                return false;
            }

            var progressed = false;
            for (int i = 0; i < maxItems && _sendQueue.Reader.TryRead(out var workItem); i++)
            {
                progressed = true;
                if (workItem.CancellationToken.IsCancellationRequested)
                {
                    workItem.Cancel();
                    continue;
                }

                if (workItem is FireAndForgetWorkItem send)
                {
                    ProcessSend(send, connection);
                }
                else if (workItem is SendWithReplyWorkItem sendWithReply)
                {
                    ProcessSendWithReply(sendWithReply, connection);
                }

                if (LibDbus.dbus_connection_has_messages_to_send(connection) != 0)
                {
                    return false;
                }
            }

            return progressed;
        }
        catch (Exception ex)
        {
            FailConnection(ex);
            return true;
        }
    }

    private bool WorkerDrainMessages()
    {
        var connection = _connection;
        if (connection == null || _disposed || _workerFailure != null)
        {
            return false;
        }

        try
        {
            return DrainMessagesUnsafe((IntPtr)connection, useReadWrite: !_watchConfigured) > 0;
        }
        catch (Exception ex)
        {
            FailConnection(ex);
            return false;
        }
    }

    private bool IsActive => !_disposed && _connection != null && _workerFailure == null;

    private void ThrowIfDisposedOrFailed()
    {
        if (_workerFailure != null)
        {
            throw new InvalidOperationException("D-Bus connection failed.", _workerFailure);
        }

        lock (_gate)
        {
            if (_disposed || _connection == null)
            {
                throw new ObjectDisposedException(nameof(DBusWireConnection));
            }
        }
    }

    private void ConfigureWatchFunctions(DBusNativeConnection* connection)
    {
        if (_watchConfigured)
        {
            return;
        }

        void* data = (void*)GCHandle.ToIntPtr(_watchHandle);
        if (LibDbus.dbus_connection_set_watch_functions(connection, s_addWatchPtr, s_removeWatchPtr, s_toggleWatchPtr, data, IntPtr.Zero) == 0)
        {
            throw new InvalidOperationException("Failed to configure D-Bus watch functions.");
        }

        LibDbus.dbus_connection_set_wakeup_main_function(connection, s_wakeupPtr, data, IntPtr.Zero);
        LibDbus.dbus_connection_set_dispatch_status_function(connection, s_dispatchPtr, data, IntPtr.Zero);

        _watchConfigured = true;
    }

    private static uint AddWatchCallback(DBusWatch* watch, void* data)
    {
        if (data == null)
        {
            return 0;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection connection)
        {
            return 0;
        }

        return connection.AddWatch(watch) ? 1u : 0u;
    }

    private static void RemoveWatchCallback(DBusWatch* watch, void* data)
    {
        if (data == null)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection connection)
        {
            return;
        }

        connection.RemoveWatch(watch);
    }

    private static void ToggleWatchCallback(DBusWatch* watch, void* data)
    {
        if (data == null)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection connection)
        {
            return;
        }

        connection.ToggleWatch(watch);
    }

    private static void WakeupCallback(void* data)
    {
        if (data == null)
        {
            return;
        }

        s_worker.Wakeup();
    }

    private static void DispatchStatusCallback(DBusNativeConnection* connection, DBusDispatchStatus newStatus, void* data)
    {
        if (data == null)
        {
            return;
        }

        if (newStatus == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS)
        {
            s_worker.Wakeup();
        }
    }

    private bool AddWatch(DBusWatch* watch)
    {
        if (watch == null)
        {
            return false;
        }

        if (!TryGetWatchFd(watch, out int fd))
        {
            return false;
        }

        var watchPtr = (IntPtr)watch;
        var flags = LibDbus.dbus_watch_get_flags(watch);
        var enabled = LibDbus.dbus_watch_get_enabled(watch) != 0;
        var events = ToPollEvents(flags);

        lock (_watchGate)
        {
            _watches[watchPtr] = new WatchState(fd, events, enabled);
        }

        s_worker.Wakeup();
        return true;
    }

    private void RemoveWatch(DBusWatch* watch)
    {
        if (watch == null)
        {
            return;
        }

        var watchPtr = (IntPtr)watch;
        lock (_watchGate)
        {
            _watches.Remove(watchPtr);
        }

        s_worker.Wakeup();
    }

    private void ToggleWatch(DBusWatch* watch)
    {
        if (watch == null)
        {
            return;
        }

        var watchPtr = (IntPtr)watch;
        var enabled = LibDbus.dbus_watch_get_enabled(watch) != 0;
        var flags = LibDbus.dbus_watch_get_flags(watch);
        var events = ToPollEvents(flags);

        lock (_watchGate)
        {
            if (_watches.TryGetValue(watchPtr, out var state))
            {
                state.Enabled = enabled;
                state.Events = events;
                _watches[watchPtr] = state;
            }
        }

        s_worker.Wakeup();
    }

    private static bool TryGetWatchFd(DBusWatch* watch, out int fd)
    {
        int localFd = LibDbus.dbus_watch_get_unix_fd(watch);
        if (localFd >= 0)
        {
            fd = localFd;
            return true;
        }

        localFd = LibDbus.dbus_watch_get_socket(watch);
        if (localFd >= 0)
        {
            fd = localFd;
            return true;
        }

        fd = -1;
        return false;
    }

    private static PollEvents ToPollEvents(uint watchFlags)
    {
        PollEvents events = PollEvents.None;
        if ((watchFlags & DBusWatchFlags.Readable) != 0)
        {
            events |= PollEvents.POLLIN;
        }
        if ((watchFlags & DBusWatchFlags.Writable) != 0)
        {
            events |= PollEvents.POLLOUT;
        }

        return events;
    }

    private static uint ToWatchFlags(PollEvents events)
    {
        uint flags = 0;
        if ((events & PollEvents.POLLIN) != 0)
        {
            flags |= DBusWatchFlags.Readable;
        }
        if ((events & PollEvents.POLLOUT) != 0)
        {
            flags |= DBusWatchFlags.Writable;
        }
        if ((events & PollEvents.POLLERR) != 0)
        {
            flags |= DBusWatchFlags.Error;
        }
        if ((events & (PollEvents.POLLHUP | PollEvents.POLLRDHUP)) != 0)
        {
            flags |= DBusWatchFlags.Hangup;
        }
        if ((events & PollEvents.POLLNVAL) != 0)
        {
            flags |= DBusWatchFlags.Error;
        }

        return flags;
    }

    private void CollectWatchItems(List<WatchItem> items)
    {
        lock (_watchGate)
        {
            foreach (var entry in _watches)
            {
                var state = entry.Value;
                if (!state.Enabled || state.Fd < 0 || state.Events == PollEvents.None)
                {
                    continue;
                }

                items.Add(new WatchItem(this, entry.Key, state.Fd, state.Events));
            }
        }
    }

    private bool HandleWatch(IntPtr watchPtr, PollEvents events)
    {
        if (watchPtr == IntPtr.Zero)
        {
            return false;
        }

        bool enabled;
        lock (_watchGate)
        {
            enabled = _watches.TryGetValue(watchPtr, out var state) && state.Enabled;
        }

        if (!enabled)
        {
            return false;
        }

        uint flags = ToWatchFlags(events);
        if (flags == 0)
        {
            return false;
        }

        if (LibDbus.dbus_watch_handle((DBusWatch*)watchPtr, flags) == 0)
        {
            FailConnection(new InvalidOperationException("dbus_watch_handle failed."));
            return true;
        }

        return true;
    }

    private void FailConnection(Exception exception)
    {
        if (_workerFailure != null)
        {
            return;
        }

        _workerFailure = exception;
        _sendQueue.Writer.TryComplete(exception);
        FailSendQueue(exception);
        CancelPendingReplies(exception);
        _incoming.Complete();
        ClearWatchList();

        DBusNativeConnection* connectionPtr;
        lock (_gate)
        {
            connectionPtr = _connection;
            _connection = null;
        }

        if (connectionPtr != null)
        {
            CloseConnection(connectionPtr, _closeOnDispose);
        }

        FreeWatchHandle();
        s_worker.RemoveConnectionImmediate(this);
    }

    private void DisposeFromWorker(DBusNativeConnection* connectionPtr, bool closeOnDispose, TaskCompletionSource completion)
    {
        try
        {
            s_worker.RemoveConnectionImmediate(this);
            FailSendQueue(new ObjectDisposedException(nameof(DBusWireConnection)));
            CancelPendingReplies();
            _incoming.Complete();
            ClearWatchList();
            CloseConnection(connectionPtr, closeOnDispose);
            FreeWatchHandle();
            completion.TrySetResult();
        }
        catch (Exception ex)
        {
            completion.TrySetException(ex);
        }
    }

    private static void CloseConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        if (connection == null)
        {
            return;
        }

        if (closeOnDispose)
        {
            LibDbus.dbus_connection_close(connection);
        }
        LibDbus.dbus_connection_unref(connection);
    }

    private void FailSendQueue(Exception error)
    {
        while (_sendQueue.Reader.TryRead(out var workItem))
        {
            workItem.Fail(error);
        }
    }

    private void ClearWatchList()
    {
        lock (_watchGate)
        {
            _watches.Clear();
        }
    }

    private void FreeWatchHandle()
    {
        if (_watchHandleAllocated)
        {
            _watchHandle.Free();
            _watchHandleAllocated = false;
        }
    }

    private void CancelPendingReplies(Exception? error = null)
    {
        List<TaskCompletionSource<DBusMessage>> pending;
        lock (_pendingGate)
        {
            if (_pendingReplies.Count == 0)
            {
                return;
            }

            pending = new List<TaskCompletionSource<DBusMessage>>(_pendingReplies.Values);
            _pendingReplies.Clear();
        }

        var exception = error ?? new ObjectDisposedException(nameof(DBusWireConnection));
        foreach (var tcs in pending)
        {
            tcs.TrySetException(exception);
        }
    }


    private static DBusWireConnection OpenBus(DBusBusType busType)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        LibDbus.dbus_error_init(&error);

        DBusNativeConnection* connection = LibDbus.dbus_bus_get(busType, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to connect to D-Bus bus.");
        }

        LibDbus.dbus_connection_set_exit_on_disconnect(connection, 0);
        return new DBusWireConnection(connection, closeOnDispose: false);
    }

    private static DBusWireConnection OpenAddress(string address)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        LibDbus.dbus_error_init(&error);

        using var addressUtf8 = new Utf8String(address);
        DBusNativeConnection* connection = LibDbus.dbus_connection_open_private(addressUtf8.Pointer, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to open D-Bus connection.");
        }

        if (LibDbus.dbus_bus_register(connection, &error) == 0)
        {
            LibDbus.dbus_connection_close(connection);
            LibDbus.dbus_connection_unref(connection);
            ThrowErrorAndFree(ref error, "Failed to register D-Bus connection.");
        }

        LibDbus.dbus_connection_set_exit_on_disconnect(connection, 0);
        return new DBusWireConnection(connection, closeOnDispose: true);
    }

    internal int DrainMessagesUnsafe(IntPtr connectionPtr, bool useReadWrite = true)
    {
        DBusNativeConnection* connection = (DBusNativeConnection*)connectionPtr;
        if (useReadWrite)
        {
            LibDbus.dbus_connection_read_write(connection, 0);
        }

        int processed = 0;
        DBusNativeMessage* message;
        while ((message = LibDbus.dbus_connection_pop_message(connection)) != null)
        {
            try
            {
                processed++;
                var managed = DBusMessageMarshaler.FromNative(message);
                if (managed.Type == DBusMessageType.MethodReturn || managed.Type == DBusMessageType.Error)
                {
                    if (managed.ReplySerial != 0 && TryRemovePending(managed.ReplySerial, out var pending))
                    {
                        LogVerbose($"SendWithReply reply: type={managed.Type} replySerial={managed.ReplySerial} error='{managed.ErrorName}' body={managed.Body.Count}");
                        pending.TrySetResult(managed);
                        continue;
                    }

                    LogVerbose($"Unmatched reply: type={managed.Type} replySerial={managed.ReplySerial} error='{managed.ErrorName}' body={managed.Body.Count}");
                }
                if (managed.Type == DBusMessageType.MethodCall)
                {
                    LogVerbose($"Receive METHOD_CALL: path='{managed.Path}' iface='{managed.Interface}' member='{managed.Member}'");
                }
                else if (managed.Type == DBusMessageType.Signal)
                {
                    LogVerbose($"Receive SIGNAL: path='{managed.Path}' iface='{managed.Interface}' member='{managed.Member}'");
                }
                _incoming.Enqueue(managed);
            }
            finally
            {
                LibDbus.dbus_message_unref(message);
            }
        }

        return processed;
    }

    private bool TryRemovePending(uint serial, out TaskCompletionSource<DBusMessage> pending)
    {
        lock (_pendingGate)
        {
            if (_pendingReplies.TryGetValue(serial, out pending!))
            {
                _pendingReplies.Remove(serial);
                return true;
            }
        }

        pending = null!;
        return false;
    }

    private static void LogVerbose(string message)
    {
        if (!s_verbose)
        {
            return;
        }

        Console.Error.WriteLine($"[DBusWire {Environment.CurrentManagedThreadId}] {message}");
    }

    private static void ThrowErrorAndFree(ref DBusError error, string fallbackMessage)
    {
        string name = error.name != null ? DbusHelpers.PtrToString(error.name) : "DBus error";
        string message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;
        LogVerbose($"libdbus error: {name}: {message}");
        fixed (DBusError* errorPtr = &error)
        {
            if (LibDbus.dbus_error_is_set(errorPtr) != 0)
            {
                LibDbus.dbus_error_free(errorPtr);
            }
        }

        throw new InvalidOperationException($"{name}: {message}");
    }

    private readonly struct WatchItem
    {
        public WatchItem(DBusWireConnection connection, IntPtr watch, int fd, PollEvents events)
        {
            Connection = connection;
            Watch = watch;
            Fd = fd;
            Events = events;
        }

        public DBusWireConnection Connection { get; }
        public IntPtr Watch { get; }
        public int Fd { get; }
        public PollEvents Events { get; }
    }

    private struct WatchState
    {
        public WatchState(int fd, PollEvents events, bool enabled)
        {
            Fd = fd;
            Events = events;
            Enabled = enabled;
        }

        public int Fd { get; set; }
        public PollEvents Events { get; set; }
        public bool Enabled { get; set; }
    }

    private static class DBusWatchFlags
    {
        public const uint Readable = 1;
        public const uint Writable = 2;
        public const uint Error = 4;
        public const uint Hangup = 8;
    }

    private sealed class SharedWorker
    {
        private const int SendBatchSize = 64;
        private readonly ConcurrentQueue<Action> _pending = new();
        private readonly List<DBusWireConnection> _connections = new();
        private readonly WakeupFd _wakeup = new();

        public SharedWorker()
        {
            var thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "DBusWireConnection"
            };
            thread.Start();
        }

        public void Register(DBusWireConnection connection)
            => Enqueue(() => _connections.Add(connection));

        public void ScheduleDispose(DBusWireConnection connection, DBusNativeConnection* connectionPtr, bool closeOnDispose, TaskCompletionSource completion)
            => Enqueue(() => connection.DisposeFromWorker(connectionPtr, closeOnDispose, completion));

        public void Enqueue(Action action)
        {
            _pending.Enqueue(action);
            _wakeup.Set();
        }

        public void Wakeup()
            => _wakeup.Set();

        public void RemoveConnectionImmediate(DBusWireConnection connection)
            => _connections.Remove(connection);

        private void Run()
        {
            var snapshot = new List<DBusWireConnection>();
            var watchItems = new List<WatchItem>();

            while (true)
            {
                DrainPending();

                snapshot.Clear();
                for (int i = 0; i < _connections.Count; i++)
                {
                    var connection = _connections[i];
                    if (connection.IsActive)
                    {
                        snapshot.Add(connection);
                    }
                }

                var progressed = false;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i].WorkerDrainSendQueue(SendBatchSize))
                    {
                        progressed = true;
                    }
                }

                var processed = false;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i].WorkerDrainMessages())
                    {
                        processed = true;
                    }
                }

                if (processed || progressed)
                {
                    continue;
                }

                unsafe
                {
                    watchItems.Clear();
                    for (int i = 0; i < snapshot.Count; i++)
                    {
                        snapshot[i].CollectWatchItems(watchItems);
                    }

                    int count = watchItems.Count + 1;
                    var fdsArray = ArrayPool<PollFd>.Shared.Rent(count);
                    try
                    {
                        fdsArray[0] = new PollFd
                        {
                            fd = _wakeup.PollFd,
                            events = PollEvents.POLLIN
                        };

                        var errorMask = PollEvents.POLLERR | PollEvents.POLLHUP | PollEvents.POLLNVAL | PollEvents.POLLRDHUP;
                        for (int i = 0; i < watchItems.Count; i++)
                        {
                            fdsArray[i + 1] = new PollFd
                            {
                                fd = watchItems[i].Fd,
                                events = watchItems[i].Events | errorMask
                            };
                        }

                        fixed (PollFd* fds = fdsArray)
                        {
                            try
                            {
                                DoPoll(fds, count);
                            }
                            catch (Exception ex)
                            {
                                LogVerbose($"Poll failed: {ex.GetType().Name}: {ex.Message}");
                                FailSnapshot(snapshot, ex);
                                continue;
                            }
                        }

                        if ((fdsArray[0].revents & PollEvents.POLLIN) != 0)
                        {
                            _wakeup.Clear();
                            DrainPending();
                        }

                        var handled = false;
                        for (int i = 0; i < watchItems.Count; i++)
                        {
                            var revents = fdsArray[i + 1].revents;
                            if (revents != 0)
                            {
                                if (watchItems[i].Connection.HandleWatch(watchItems[i].Watch, revents))
                                {
                                    handled = true;
                                }
                            }
                        }

                        if (handled)
                        {
                            continue;
                        }
                    }
                    finally
                    {
                        ArrayPool<PollFd>.Shared.Return(fdsArray, clearArray: true);
                    }
                }
            }
        }

        private void DrainPending()
        {
            while (_pending.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    LogVerbose($"Worker action failed: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        private static unsafe void DoPoll(PollFd* fds, int count)
        {
            while (true)
            {
                int pollRet = LinuxPoll.ppoll(fds, new IntPtr(count), IntPtr.Zero, IntPtr.Zero);
                if (pollRet > 0)
                {
                    return;
                }

                if (pollRet == 0)
                {
                    return;
                }

                int errno = Marshal.GetLastPInvokeError();
                if (errno == LinuxPoll.EINTR)
                {
                    continue;
                }

                throw new InvalidOperationException($"ppoll failed with errno {errno}.");
            }
        }

        private static void FailSnapshot(List<DBusWireConnection> snapshot, Exception exception)
        {
            for (int i = 0; i < snapshot.Count; i++)
            {
                snapshot[i].FailConnection(exception);
            }
        }
    }

    private abstract class SendWorkItem
    {
        protected SendWorkItem(DBusMessage message, CancellationToken cancellationToken)
        {
            Message = message;
            CancellationToken = cancellationToken;
        }

        public DBusMessage Message { get; }
        public CancellationToken CancellationToken { get; }

        public abstract void Cancel();
        public abstract void Fail(Exception exception);
    }

    private sealed class FireAndForgetWorkItem : SendWorkItem
    {
        private readonly TaskCompletionSource _completion;

        public FireAndForgetWorkItem(DBusMessage message, TaskCompletionSource completion, CancellationToken cancellationToken)
            : base(message, cancellationToken)
        {
            _completion = completion;
        }

        public void Complete()
            => _completion.TrySetResult();

        public override void Cancel()
            => _completion.TrySetCanceled(CancellationToken);

        public override void Fail(Exception exception)
            => _completion.TrySetException(exception);
    }

    private sealed class SendWithReplyWorkItem : SendWorkItem
    {
        public SendWithReplyWorkItem(
            DBusMessage message,
            TaskCompletionSource<DBusMessage> completion,
            CancellationToken cancellationToken,
            DateTime startTimestamp)
            : base(message, cancellationToken)
        {
            Completion = completion;
            StartTimestamp = startTimestamp;
        }

        public TaskCompletionSource<DBusMessage> Completion { get; }
        public DateTime StartTimestamp { get; }

        public override void Cancel()
            => Completion.TrySetCanceled(CancellationToken);

        public override void Fail(Exception exception)
            => Completion.TrySetException(exception);
    }
}
