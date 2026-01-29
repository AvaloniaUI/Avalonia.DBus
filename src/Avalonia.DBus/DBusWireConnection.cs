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
using DBusWatchPtr = System.IntPtr;
using OtherPtr = System.IntPtr;

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

    private static readonly bool s_verbose =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE"));

    private static readonly DBusAddWatchFunction s_addWatchCallback = AddWatchCallback;
    private static readonly DBusRemoveWatchFunction s_removeWatchCallback = RemoveWatchCallback;
    private static readonly DBusWatchToggledFunction s_toggleWatchCallback = ToggleWatchCallback;
    private static readonly DBusWakeupMainFunction s_wakeupCallback = WakeupCallback;
    private static readonly DBusDispatchStatusFunction s_dispatchCallback = DispatchStatusCallback;
    private static readonly DBusFreeFunction s_freeCallback= FreeCallback;


    private static readonly IntPtr s_addWatchPtr = Marshal.GetFunctionPointerForDelegate(s_addWatchCallback);
    private static readonly IntPtr s_removeWatchPtr = Marshal.GetFunctionPointerForDelegate(s_removeWatchCallback);
    private static readonly IntPtr s_toggleWatchPtr = Marshal.GetFunctionPointerForDelegate(s_toggleWatchCallback);
    private static readonly IntPtr s_wakeupPtr = Marshal.GetFunctionPointerForDelegate(s_wakeupCallback);
    private static readonly IntPtr s_dispatchPtr = Marshal.GetFunctionPointerForDelegate(s_dispatchCallback);
    private static readonly IntPtr s_freePtr = Marshal.GetFunctionPointerForDelegate(s_freeCallback);

    private readonly AsyncMessageQueue _incoming = new();
    private readonly TaskCompletionSource _disposeCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentDictionary<DBusWatchPtr, WatchState> _watches = new();
    private DBusNativeConnection* _connection;
    private readonly bool _closeOnDispose;
    private bool _disposed;
    private Exception? _workerFailure; 
    private void* _wireInstPtr =  null;
    private Thread? _workerThread;

    private DBusWireConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The D-Bus wire connection requires Linux polling.");
        }

        _connection = connection;
        _closeOnDispose = closeOnDispose;
 
        ConfigureWatchFunctions(connection); 
        
        using var signalMatch = new Utf8String("type='signal'");
        using var methodMatch = new Utf8String("type='method_call'");
        
        LibDbus.dbus_bus_add_match(connection, signalMatch.Pointer, null);
        LibDbus.dbus_bus_add_match(connection, methodMatch.Pointer, null);

        StartEventLoop();
    }

    private void StartEventLoop()
    {
        _workerThread = new Thread(MainLoop)
        {
            IsBackground = true,
            Name = $"DBusWireConnection_{GetHashCode()}"
        };
        _workerThread.Start();
    }

    private void MainLoop()
    {
        while (!_disposed)
        {
            foreach (var watch in _watches.Values)
            {
                
                LibDbus.dbus_watch_get_enabled(watch.);
                watch.Fd();
            }
        }
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
            string? uniqueName = null;
            lock (_gate)
                if (_connection != null)
                    uniqueName = DbusHelpers.PtrToStringNullable(LibDbus.dbus_bus_get_unique_name(_connection));
            return uniqueName;
        }
    }

    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    public ValueTask SendAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

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
        LogVerbose(
            $"SendWithReply begin: dest='{message.Destination}' path='{message.Path}' iface='{message.Interface}' member='{message.Member}' body={message.Body.Count}");

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

        s_worker.ScheduleDispose(this, connectionPtr, _closeOnDispose, _disposeCompletion);
        return new ValueTask(_disposeCompletion.Task);
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
        try
        {
            _wireInstPtr = (void*)GCHandle.Alloc(this).AddrOfPinnedObject();

            if (LibDbus.dbus_connection_set_watch_functions(connection, s_addWatchPtr, s_removeWatchPtr,
                    s_toggleWatchPtr,
                    _wireInstPtr, s_freePtr) == 0)
            {
                throw new InvalidOperationException("Failed to configure D-Bus watch functions.");
            }

            LibDbus.dbus_connection_set_wakeup_main_function(connection, s_wakeupPtr, _wireInstPtr, s_freePtr);
            LibDbus.dbus_connection_set_dispatch_status_function(connection, s_dispatchPtr, _wireInstPtr, s_freePtr);
        }
        catch (Exception e)
        {
            LogVerbose(e.ToString());
        }
        finally
        {
            FreeCallback(_wireInstPtr);
        } 
    }

    private static uint AddWatchCallback(DBusWatch* watch, void* data)
    {
        if (data == null)
        {
            return 0;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
        {
            return 0;
        }

        return wire.AddWatch(watch) ? 1u : 0u;
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
        if (handle.Target is not DBusWireConnection wire)
        {
            return;
        }

        wire.ToggleWatch(watch);
    }

    private static void WakeupCallback(void* data)
    {
        if (data == null)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
        {
            return;
        }

        wire.Wakeup();
    }

    private void Wakeup()
    {

    }

    private static void DispatchStatusCallback(DBusNativeConnection* conn, DBusDispatchStatus newStatus,
        void* data)
    {
        if (data == null)
        {
            return;
        }
        
        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
        {
            return;
        }
        
        if (newStatus == DBusDispatchStatus.DBUS_DISPATCH_DATA_REMAINS)
        {
            wire.Wakeup();
        }
    }
    
    
    private static void FreeCallback(void* data)
    {
        if (data == null)
        {
            return;
        }
        
        var handle = GCHandle.FromIntPtr((IntPtr)data);
        if (handle.Target is not DBusWireConnection wire)
        {
            return;
        }

        handle.Free();
    }

    private bool AddWatch(DBusWatch* watch)
    {
        if (watch == null)
        {
            return false;
        }

        if (!TryGetWatchFd(watch, out var fd))
        {
            return false;
        }

        var watchPtr = (IntPtr)watch;
        var flags = LibDbus.dbus_watch_get_flags(watch);
        var enabled = LibDbus.dbus_watch_get_enabled(watch) != 0;
        var events = ToPollEvents(flags);
 
            _watches[watchPtr] = new WatchState(fd, events, enabled);
       

        return true;
    }

    private void RemoveWatch(DBusWatch* watch)
    {
        if (watch == null)
        {
            return;
        }

        var watchPtr = (IntPtr)watch; 
            _watches.Remove(watchPtr, out var value); 

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
 
            if (_watches.TryGetValue(watchPtr, out var state))
            {
                state.Enabled = enabled;
                state.Events = events;
                _watches[watchPtr] = state;
            } 

        s_worker.Wakeup();
    }

    private static bool TryGetWatchFd(DBusWatch* watch, out int fd)
    {
        var localFd = LibDbus.dbus_watch_get_unix_fd(watch);
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

        var flags = ToWatchFlags(events);
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


    private static DBusWireConnection OpenBus(DBusBusType busType)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        LibDbus.dbus_error_init(&error);

        var connection = LibDbus.dbus_bus_get(busType, &error);
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
        var connection = LibDbus.dbus_connection_open_private(addressUtf8.Pointer, &error);
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
        var name = error.name != null ? DbusHelpers.PtrToString(error.name) : "DBus error";
        var message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;
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

    private readonly struct WatchItem(DBusWireConnection connection, IntPtr watch, int fd, PollEvents events)
    {
        public DBusWireConnection Connection { get; } = connection;
        public IntPtr Watch { get; } = watch;
        public int Fd { get; } = fd;
        public PollEvents Events { get; } = events;
    }

    private struct WatchState(int fd, PollEvents events, bool enabled)
    {
        public int Fd { get; set; } = fd;
        public PollEvents Events { get; set; } = events;
        public bool Enabled { get; set; } = enabled;
    }
 

    private abstract class SendWorkItem(DBusMessage message, CancellationToken cancellationToken)
    {
        public DBusMessage Message { get; } = message;
        public CancellationToken CancellationToken { get; } = cancellationToken;

        public abstract void Cancel();
        public abstract void Fail(Exception exception);
    }

    private sealed class FireAndForgetWorkItem(
        DBusMessage message,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
        : SendWorkItem(message, cancellationToken)
    {
        public void Complete()
            => completion.TrySetResult();

        public override void Cancel()
            => completion.TrySetCanceled(CancellationToken);

        public override void Fail(Exception exception)
            => completion.TrySetException(exception);
    }

    private sealed class SendWithReplyWorkItem(
        DBusMessage message,
        TaskCompletionSource<DBusMessage> completion,
        CancellationToken cancellationToken,
        DateTime startTimestamp)
        : SendWorkItem(message, cancellationToken)
    {
        public TaskCompletionSource<DBusMessage> Completion { get; } = completion;
        public DateTime StartTimestamp { get; } = startTimestamp;

        public override void Cancel()
            => Completion.TrySetCanceled(CancellationToken);

        public override void Fail(Exception exception)
            => Completion.TrySetException(exception);
    }
}