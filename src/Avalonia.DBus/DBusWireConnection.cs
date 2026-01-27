using System;
using System.Collections.Generic;
using System.Threading;
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
    private readonly object _ioGate = new();
    private readonly object _pendingGate = new();
    private readonly Dictionary<uint, TaskCompletionSource<DBusMessage>> _pendingReplies = new();
    private static readonly bool s_verbose = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE"));
    private readonly AsyncMessageQueue _incoming = new();
    private readonly CancellationTokenSource _receiveCts = new();
    private readonly Task _receiveLoop;
    private DBusNativeConnection* _connection;
    private readonly bool _closeOnDispose;
    private bool _disposed;

    private DBusWireConnection(DBusNativeConnection* connection, bool closeOnDispose)
    {
        _connection = connection;
        _closeOnDispose = closeOnDispose;
        _receiveLoop = Task.Run(() => ReceiveLoop(_receiveCts.Token));
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
            DBusNativeConnection* connection = GetConnectionOrThrow();
            return DbusHelpers.PtrToStringNullable(LibDbus.dbus_bus_get_unique_name(connection));
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

        DBusNativeConnection* connection = BorrowConnection();
        LogVerbose("SendWithReply marshaling start");
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(message);
        LogVerbose("SendWithReply marshaling done");
        try
        {
            lock (_ioGate)
            {
                uint serial = 0;
                if (LibDbus.dbus_connection_send(connection, native, &serial) == 0)
                {
                    throw new InvalidOperationException("Failed to send D-Bus message.");
                }

                if (serial != 0)
                {
                    message.Serial = serial;
                }

                LibDbus.dbus_connection_flush(connection);
            }
        }
        finally
        {
            LibDbus.dbus_message_unref(native);
            ReleaseBorrowed(connection);
        }

        return default;
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
        LogVerbose($"SendWithReply begin: dest='{message.Destination}' path='{message.Path}' iface='{message.Interface}' member='{message.Member}' body={message.Body.Count}");

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startTimestamp = DateTime.UtcNow;
        uint serial;
        DBusNativeConnection* connection = BorrowConnection();
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(message);
        try
        {
            if (!Monitor.TryEnter(_ioGate))
            {
                LogVerbose("SendWithReply waiting for IO gate");
                Monitor.Enter(_ioGate);
            }
            try
            {
                LogVerbose("SendWithReply sending...");
                serial = 0;
                if (LibDbus.dbus_connection_send(connection, native, &serial) == 0)
                {
                    LogVerbose("SendWithReply send failed (dbus_connection_send returned 0)");
                    throw new InvalidOperationException("Failed to send D-Bus message.");
                }

                if (serial != 0)
                {
                    message.Serial = serial;
                }
                LogVerbose($"SendWithReply send ok: serial={serial}");

                lock (_pendingGate)
                {
                    _pendingReplies[serial] = tcs;
                }

                LogVerbose("SendWithReply flushing...");
                LibDbus.dbus_connection_flush(connection);
                LogVerbose("SendWithReply flush complete");
            }
            finally
            {
                Monitor.Exit(_ioGate);
            }
        }
        catch (Exception ex)
        {
            LogVerbose($"SendWithReply failed: {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            LibDbus.dbus_message_unref(native);
            ReleaseBorrowed(connection);
        }

        if (cancellationToken.CanBeCanceled)
        {
            _ = cancellationToken.Register(state =>
            {
                var tuple = (Tuple<DBusWireConnection, uint>)state!;
                if (tuple.Item1.TryRemovePending(tuple.Item2, out var pending))
                {
                    pending.TrySetCanceled();
                }
            }, Tuple.Create(this, serial));
        }

        var elapsed = DateTime.UtcNow - startTimestamp;
        LogVerbose($"SendWithReply sent: serial={serial} after {elapsed.TotalMilliseconds:0} ms");
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
        IntPtr connectionPtr;
        lock (_gate)
        {
            if (_disposed)
            {
                return default;
            }
            _disposed = true;
            connectionPtr = (IntPtr)_connection;
            _connection = null;
        }

        _receiveCts.Cancel();
        _incoming.Complete();
        CancelPendingReplies();

        Task completion = _receiveLoop.ContinueWith(
            t => FinalizeDispose(t, connectionPtr),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return new ValueTask(completion);
    }

    private void CancelPendingReplies()
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

        var exception = new ObjectDisposedException(nameof(DBusWireConnection));
        foreach (var tcs in pending)
        {
            tcs.TrySetException(exception);
        }
    }

    private void FinalizeDispose(Task completed, IntPtr connectionPtr)
    {
        _ = completed.Exception;

        DBusNativeConnection* connection = (DBusNativeConnection*)connectionPtr;
        if (connection != null)
        {
            if (_closeOnDispose)
            {
                LibDbus.dbus_connection_close(connection);
            }
            LibDbus.dbus_connection_unref(connection);
        }

        _receiveCts.Dispose();
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

    private void ReceiveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DBusNativeConnection* connection;
            lock (_gate)
            {
                if (_disposed || _connection == null)
                {
                    return;
                }
                connection = _connection;
            }

            if (!Monitor.TryEnter(_ioGate))
            {
                LogVerbose("ReceiveLoop waiting for IO gate");
                Monitor.Enter(_ioGate);
            }
            try
            {
                LibDbus.dbus_connection_read_write(connection, 100);

                DBusNativeMessage* message;
                while ((message = LibDbus.dbus_connection_pop_message(connection)) != null)
                {
                    try
                    {
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
            }
            finally
            {
                Monitor.Exit(_ioGate);
            }
        }
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

    private DBusNativeConnection* GetConnectionOrThrow()
    {
        lock (_gate)
        {
            if (_disposed || _connection == null)
            {
                throw new ObjectDisposedException(nameof(DBusWireConnection));
            }

            return _connection;
        }
    }

    private DBusNativeConnection* BorrowConnection()
    {
        lock (_gate)
        {
            if (_disposed || _connection == null)
            {
                throw new ObjectDisposedException(nameof(DBusWireConnection));
            }

            LibDbus.dbus_connection_ref(_connection);
            return _connection;
        }
    }

    private static void ReleaseBorrowed(DBusNativeConnection* connection)
    {
        if (connection != null)
        {
            LibDbus.dbus_connection_unref(connection);
        }
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
}
