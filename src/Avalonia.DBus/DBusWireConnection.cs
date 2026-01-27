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
        return Task.FromResult(OpenBus(DBusBusType.Session));
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<DBusWireConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(OpenBus(DBusBusType.System));
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
            return DbusHelpers.PtrToStringNullable(dbus.dbus_bus_get_unique_name(connection));
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
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(message);
        try
        {
            uint serial = 0;
            if (dbus.dbus_connection_send(connection, native, &serial) == 0)
            {
                throw new InvalidOperationException("Failed to send D-Bus message.");
            }

            if (serial != 0)
            {
                message.Serial = serial;
            }

            dbus.dbus_connection_flush(connection);
        }
        finally
        {
            dbus.dbus_message_unref(native);
            ReleaseBorrowed(connection);
        }

        return ValueTask.CompletedTask;
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
        return Task.Run(() => SendWithReplyInternal(message), cancellationToken);
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
    public async ValueTask DisposeAsync()
    {
        DBusNativeConnection* connection;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            connection = _connection;
            _connection = null;
        }

        _receiveCts.Cancel();
        _incoming.Complete();

        try
        {
            await _receiveLoop.ConfigureAwait(false);
        }
        catch
        {
            // Ignore receive loop failures on shutdown.
        }

        if (connection != null)
        {
            if (_closeOnDispose)
            {
                dbus.dbus_connection_close(connection);
            }
            dbus.dbus_connection_unref(connection);
        }

        _receiveCts.Dispose();
    }

    private static DBusWireConnection OpenBus(DBusBusType busType)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        dbus.dbus_error_init(&error);

        DBusNativeConnection* connection = dbus.dbus_bus_get(busType, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to connect to D-Bus bus.");
        }

        dbus.dbus_connection_set_exit_on_disconnect(connection, 0);
        return new DBusWireConnection(connection, closeOnDispose: false);
    }

    private static DBusWireConnection OpenAddress(string address)
    {
        DbusHelpers.EnsureThreadsInitialized();
        DBusError error = default;
        dbus.dbus_error_init(&error);

        using var addressUtf8 = new Utf8String(address);
        DBusNativeConnection* connection = dbus.dbus_connection_open_private(addressUtf8.Pointer, &error);
        if (connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to open D-Bus connection.");
        }

        if (dbus.dbus_bus_register(connection, &error) == 0)
        {
            dbus.dbus_connection_close(connection);
            dbus.dbus_connection_unref(connection);
            ThrowErrorAndFree(ref error, "Failed to register D-Bus connection.");
        }

        dbus.dbus_connection_set_exit_on_disconnect(connection, 0);
        return new DBusWireConnection(connection, closeOnDispose: true);
    }

    private DBusMessage SendWithReplyInternal(DBusMessage message)
    {
        DBusNativeConnection* connection = BorrowConnection();
        DBusNativeMessage* native = DBusMessageMarshaler.ToNative(message);
        try
        {
            DBusError error = default;
            dbus.dbus_error_init(&error);

            DBusNativeMessage* reply = dbus.dbus_connection_send_with_reply_and_block(connection, native, -1, &error);
            uint serial = dbus.dbus_message_get_serial(native);
            if (serial != 0)
            {
                message.Serial = serial;
            }

            dbus.dbus_message_unref(native);

            if (reply == null)
            {
                ThrowErrorAndFree(ref error, "D-Bus call failed.");
            }

            try
            {
                return DBusMessageMarshaler.FromNative(reply);
            }
            finally
            {
                dbus.dbus_message_unref(reply);
            }
        }
        finally
        {
            ReleaseBorrowed(connection);
        }
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

            dbus.dbus_connection_read_write(connection, 100);

            DBusNativeMessage* message;
            while ((message = dbus.dbus_connection_pop_message(connection)) != null)
            {
                try
                {
                    _incoming.Enqueue(DBusMessageMarshaler.FromNative(message));
                }
                finally
                {
                    dbus.dbus_message_unref(message);
                }
            }
        }
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

            dbus.dbus_connection_ref(_connection);
            return _connection;
        }
    }

    private static void ReleaseBorrowed(DBusNativeConnection* connection)
    {
        if (connection != null)
        {
            dbus.dbus_connection_unref(connection);
        }
    }

    private static void ThrowErrorAndFree(ref DBusError error, string fallbackMessage)
    {
        string name = error.name != null ? DbusHelpers.PtrToString(error.name) : "DBus error";
        string message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;
        fixed (DBusError* errorPtr = &error)
        {
            if (dbus.dbus_error_is_set(errorPtr) != 0)
            {
                dbus.dbus_error_free(errorPtr);
            }
        }

        throw new InvalidOperationException($"{name}: {message}");
    }
}
