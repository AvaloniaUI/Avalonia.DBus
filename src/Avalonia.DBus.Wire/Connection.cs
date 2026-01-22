using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.AutoGen;

namespace Avalonia.DBus.Wire;

public sealed unsafe class Connection : IDisposable
{
    private static readonly bool s_verbose = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LIBDBUS_AUTOGEN_VERBOSE"));
    private static readonly Stopwatch s_uptime = Stopwatch.StartNew();
    private static long s_callId;

    private readonly object _gate = new();
    private DBusConnection* _connection;
    private readonly bool _closeOnDispose;
    private ConnectionEventLoop? _eventLoop;
    private Thread? _dispatchThread;
    private bool _dispatchRunning;
    private bool _disposed;
    private GCHandle _filterHandle;
    private bool _filterInstalled;
    private readonly List<IObserver> _observers = new();
    private readonly List<GCHandle> _pathHandles = new();

    private static readonly DBusHandleMessageFunction s_filterCallback = HandleMessageFilter;
    private static readonly IntPtr s_filterCallbackPtr = Marshal.GetFunctionPointerForDelegate(s_filterCallback);

    private static readonly DBusObjectPathMessageFunction s_pathCallback = HandlePathMessage;
    private static readonly IntPtr s_pathCallbackPtr = Marshal.GetFunctionPointerForDelegate(s_pathCallback);

    private static readonly DBusObjectPathVTable s_vtable = new()
    {
        unregister_function = IntPtr.Zero,
        message_function = s_pathCallbackPtr,
        dbus_internal_pad1 = IntPtr.Zero,
        dbus_internal_pad2 = IntPtr.Zero,
        dbus_internal_pad3 = IntPtr.Zero,
        dbus_internal_pad4 = IntPtr.Zero
    };

    public Connection(DBusBusType busType)
    {
        DbusHelpers.EnsureThreadsInitialized();
        LogVerbose($"Connecting to bus {busType}");
        Stopwatch? sw = s_verbose ? Stopwatch.StartNew() : null;
        DBusError error = default;
        dbus.dbus_error_init(&error);
        _connection = dbus.dbus_bus_get(busType, &error);
        if (_connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to connect to bus.");
        }
        _closeOnDispose = false;
        StartDispatchLoop();
        if (s_verbose && sw != null)
        {
            LogVerbose($"Connected to bus {busType} ({sw.ElapsedMilliseconds} ms)");
        }
    }

    public Connection(string address)
    {
        DbusHelpers.EnsureThreadsInitialized();
        LogVerbose("Opening private connection");
        Stopwatch? openSw = s_verbose ? Stopwatch.StartNew() : null;
        DBusError error = default;
        dbus.dbus_error_init(&error);
        using var addr = new Utf8String(address);
        _connection = dbus.dbus_connection_open_private(addr.Pointer, &error);
        if (_connection == null)
        {
            ThrowErrorAndFree(ref error, "Failed to open D-Bus connection.");
        }
        if (s_verbose && openSw != null)
        {
            LogVerbose($"Opened private connection ({openSw.ElapsedMilliseconds} ms)");
        }
        LogVerbose("Registering private connection");
        Stopwatch? registerSw = s_verbose ? Stopwatch.StartNew() : null;
        if (dbus.dbus_bus_register(_connection, &error) == 0)
        {
            ThrowErrorAndFree(ref error, "Failed to register D-Bus connection.");
        }
        if (s_verbose && registerSw != null)
        {
            LogVerbose($"Registered private connection ({registerSw.ElapsedMilliseconds} ms)");
        }
        _closeOnDispose = true;
        StartDispatchLoop();
    }

    public string UniqueName
    {
        get
        {
            if (_connection == null)
            {
                throw new ObjectDisposedException(nameof(Connection));
            }
            return DbusHelpers.PtrToString(dbus.dbus_bus_get_unique_name(_connection));
        }
    }

    public MessageWriter GetMessageWriter() => new MessageWriter(null);

    public Task CallMethodAsync(MessageBuffer message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }
        CallMethodInternal(message.Detach());
        return Task.CompletedTask;
    }

    public Task<T> CallMethodAsync<T>(MessageBuffer message, MessageValueReader<T> reader, object? readerState = null)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }
        return Task.FromResult(CallMethodInternal(message.Detach(), reader, readerState));
    }

    private void CallMethodInternal(DBusMessage* message)
    {
        long callId = s_verbose ? Interlocked.Increment(ref s_callId) : 0;
        Stopwatch? sw = s_verbose ? Stopwatch.StartNew() : null;
        string? description = s_verbose ? DescribeMessage(message) : null;
        if (s_verbose)
        {
            LogVerbose($"Call {callId} start {description}");
        }
        try
        {
            DBusConnection* connection = BorrowConnection();
            DBusError error = default;
            dbus.dbus_error_init(&error);
            DBusMessage* reply = dbus.dbus_connection_send_with_reply_and_block(connection, message, -1, &error);
            dbus.dbus_message_unref(message);
            if (reply == null)
            {
                if (s_verbose && sw != null)
                {
                    LogVerbose($"Call {callId} failed ({sw.ElapsedMilliseconds} ms)");
                }
                ThrowErrorAndFree(ref error, "Call failed.");
            }
            if (s_verbose && sw != null)
            {
                string replyDescription = DescribeMessage(reply);
                LogVerbose($"Call {callId} end ({sw.ElapsedMilliseconds} ms) reply {replyDescription}");
            }
            dbus.dbus_message_unref(reply);
            ReleaseBorrowed(connection);
        }
        catch
        {
            if (message != null)
            {
                dbus.dbus_message_unref(message);
            }
            throw;
        }
    }

    private T CallMethodInternal<T>(DBusMessage* message, MessageValueReader<T> reader, object? readerState)
    {
        long callId = s_verbose ? Interlocked.Increment(ref s_callId) : 0;
        Stopwatch? sw = s_verbose ? Stopwatch.StartNew() : null;
        string? description = s_verbose ? DescribeMessage(message) : null;
        if (s_verbose)
        {
            LogVerbose($"Call {callId} start {description}");
        }
        try
        {
            DBusConnection* connection = BorrowConnection();
            DBusError error = default;
            dbus.dbus_error_init(&error);
            DBusMessage* reply = dbus.dbus_connection_send_with_reply_and_block(connection, message, -1, &error);
            dbus.dbus_message_unref(message);
            if (reply == null)
            {
                if (s_verbose && sw != null)
                {
                    LogVerbose($"Call {callId} failed ({sw.ElapsedMilliseconds} ms)");
                }
                ThrowErrorAndFree(ref error, "Call failed.");
            }
            if (s_verbose && sw != null)
            {
                string replyDescription = DescribeMessage(reply);
                LogVerbose($"Call {callId} end ({sw.ElapsedMilliseconds} ms) reply {replyDescription}");
            }
            try
            {
                var msg = new Message(reply);
                return reader(msg, readerState);
            }
            finally
            {
                dbus.dbus_message_unref(reply);
                ReleaseBorrowed(connection);
            }
        }
        catch
        {
            if (message != null)
            {
                dbus.dbus_message_unref(message);
            }
            throw;
        }
    }

    public bool TrySendMessage(MessageBuffer message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }
        var msg = message.Detach();
        if (msg == null)
        {
            return false;
        }
        if (s_verbose)
        {
            LogVerbose($"Send {DescribeMessage(msg)}");
        }
        DBusConnection* connection;
        try
        {
            connection = BorrowConnection();
        }
        catch (ObjectDisposedException)
        {
            dbus.dbus_message_unref(msg);
            return false;
        }
        bool sent = dbus.dbus_connection_send(connection, msg, null) != 0;
        dbus.dbus_message_unref(msg);
        ReleaseBorrowed(connection);
        return sent;
    }

    public ValueTask<IDisposable> AddMatchAsync<T>(MatchRule rule, MessageValueReader<T> reader, Action<Exception?, T, object?> handler, object? readerState = null, object? handlerState = null, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
    {
        _ = flags;
        if (rule == null)
        {
            throw new ArgumentNullException(nameof(rule));
        }
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        EnsureFilterInstalled();

        string match = rule.ToMatchString();
        if (!string.IsNullOrEmpty(match))
        {
            LogVerbose($"AddMatch '{match}'");
            DBusError error = default;
            dbus.dbus_error_init(&error);
            using var matchUtf8 = new Utf8String(match);
            DBusConnection* connection = BorrowConnection();
            dbus.dbus_bus_add_match(connection, matchUtf8.Pointer, &error);
            ReleaseBorrowed(connection);
            if (dbus.dbus_error_is_set(&error) != 0)
            {
                ThrowErrorAndFree(ref error, "Failed to add match.");
            }
        }

        SynchronizationContext? context = emitOnCapturedContext ? SynchronizationContext.Current : null;
        var observer = new Observer<T>(this, rule, reader, handler, readerState, handlerState, context, match);
        lock (_gate)
        {
            _observers.Add(observer);
        }
        return new ValueTask<IDisposable>(observer);
    }

    public void RegisterPathHandler(IPathMethodHandler handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var registration = new PathRegistration(this, handler);
        GCHandle handle = GCHandle.Alloc(registration);
        _pathHandles.Add(handle);
        using var pathUtf8 = new Utf8String(handler.Path);
        fixed (DBusObjectPathVTable* vtablePtr = &s_vtable)
        {
            DBusConnection* connection = BorrowConnection();
            uint result = handler.HandlesChildPaths
                ? dbus.dbus_connection_register_fallback(connection, pathUtf8.Pointer, vtablePtr, (void*)GCHandle.ToIntPtr(handle))
                : dbus.dbus_connection_register_object_path(connection, pathUtf8.Pointer, vtablePtr, (void*)GCHandle.ToIntPtr(handle));
            ReleaseBorrowed(connection);
            if (result == 0)
            {
                handle.Free();
                throw new InvalidOperationException("Failed to register object path.");
            }
        }
    }

    public void Dispose()
    {
        DBusConnection* connection;
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

        StopDispatchLoop();

        if (_filterInstalled)
        {
            if (connection != null)
            {
                dbus.dbus_connection_remove_filter(connection, s_filterCallbackPtr, (void*)GCHandle.ToIntPtr(_filterHandle));
            }
            _filterHandle.Free();
            _filterInstalled = false;
        }

        foreach (var handle in _pathHandles)
        {
            if (handle.IsAllocated)
            {
                handle.Free();
            }
        }
        _pathHandles.Clear();

        if (connection != null)
        {
            if (_closeOnDispose)
            {
                dbus.dbus_connection_close(connection);
            }
            dbus.dbus_connection_unref(connection);
        }
    }

    private DBusConnection* BorrowConnection()
    {
        lock (_gate)
        {
            if (_disposed || _connection == null)
            {
                throw new ObjectDisposedException(nameof(Connection));
            }
            dbus.dbus_connection_ref(_connection);
            return _connection;
        }
    }

    private static void ReleaseBorrowed(DBusConnection* connection)
    {
        if (connection != null)
        {
            dbus.dbus_connection_unref(connection);
        }
    }

    private void StartDispatchLoop()
    {
        try
        {
            _eventLoop = new ConnectionEventLoop(_connection);
            LogVerbose("Event-driven dispatch enabled");
        }
        catch (Exception ex)
        {
            LogVerbose($"Event-driven dispatch unavailable ({ex.Message}); falling back to polling.");
            _dispatchRunning = true;
            _dispatchThread = new Thread(DispatchLoop)
            {
                IsBackground = true,
                Name = "Avalonia.DBus.Wire.Dispatch"
            };
            _dispatchThread.Start();
            LogVerbose("Polling dispatch loop started");
        }
    }

    private void StopDispatchLoop()
    {
        if (_eventLoop != null)
        {
            _eventLoop.Dispose();
            _eventLoop = null;
        }

        _dispatchRunning = false;
        if (_dispatchThread != null && _dispatchThread.IsAlive)
        {
            _dispatchThread.Join(500);
        }
        _dispatchThread = null;
    }

    private void DispatchLoop()
    {
        while (_dispatchRunning)
        {
            if (_connection == null)
            {
                return;
            }
            dbus.dbus_connection_read_write_dispatch(_connection, 100);
        }
    }

    private void EnsureFilterInstalled()
    {
        if (_filterInstalled)
        {
            return;
        }

        _filterHandle = GCHandle.Alloc(this);
        dbus.dbus_connection_add_filter(_connection, s_filterCallbackPtr, (void*)GCHandle.ToIntPtr(_filterHandle), IntPtr.Zero);
        _filterInstalled = true;
    }

    private static DBusHandlerResult HandleMessageFilter(DBusConnection* connection, DBusMessage* message, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (!handle.IsAllocated || handle.Target is not Connection managed)
        {
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }
        managed.DispatchMessage(message);
        return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
    }

    private void DispatchMessage(DBusMessage* message)
    {
        List<IObserver> snapshot;
        lock (_gate)
        {
            snapshot = new List<IObserver>(_observers);
        }

        foreach (var observer in snapshot)
        {
            if (observer.IsMatch(message))
            {
                observer.Invoke(message);
            }
        }
    }

    private static DBusHandlerResult HandlePathMessage(DBusConnection* connection, DBusMessage* message, void* userData)
    {
        var handle = GCHandle.FromIntPtr((IntPtr)userData);
        if (!handle.IsAllocated || handle.Target is not PathRegistration registration)
        {
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }

        if (dbus.dbus_message_get_type(message) != dbus.DBUS_MESSAGE_TYPE_METHOD_CALL)
        {
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }

        try
        {
            var context = new MethodContext(registration.Connection, message);
            var result = registration.Handler.HandleMethodAsync(context);
            if (!result.IsCompletedSuccessfully)
            {
                result.AsTask().GetAwaiter().GetResult();
            }
            return DBusHandlerResult.DBUS_HANDLER_RESULT_HANDLED;
        }
        catch
        {
            return DBusHandlerResult.DBUS_HANDLER_RESULT_NOT_YET_HANDLED;
        }
    }

    private static void ThrowErrorAndFree(ref DBusError error, string fallbackMessage)
    {
        string name = error.name != null ? DbusHelpers.PtrToString(error.name) : "DBus error";
        string message = error.message != null ? DbusHelpers.PtrToString(error.message) : fallbackMessage;
        LogVerbose($"DBus error: {name}: {message}");
        fixed (DBusError* errorPtr = &error)
        {
            if (dbus.dbus_error_is_set(errorPtr) != 0)
            {
                dbus.dbus_error_free(errorPtr);
            }
        }
        throw new DBusException(name, message);
    }

    private static void LogVerbose(string message)
    {
        if (!s_verbose)
        {
            return;
        }
        Console.Error.WriteLine($"[DBus {s_uptime.Elapsed:hh\\:mm\\:ss\\.fff}] {message}");
    }

    private static string DescribeMessage(DBusMessage* message)
    {
        if (message == null)
        {
            return "(null message)";
        }

        string sender = DbusHelpers.PtrToString(dbus.dbus_message_get_sender(message));
        string dest = DbusHelpers.PtrToString(dbus.dbus_message_get_destination(message));
        string path = DbusHelpers.PtrToString(dbus.dbus_message_get_path(message));
        string iface = DbusHelpers.PtrToString(dbus.dbus_message_get_interface(message));
        string member = DbusHelpers.PtrToString(dbus.dbus_message_get_member(message));
        string signature = DbusHelpers.PtrToString(dbus.dbus_message_get_signature(message));
        int type = dbus.dbus_message_get_type(message);
        string typeName = MessageTypeToString(type);

        if (type == dbus.DBUS_MESSAGE_TYPE_ERROR)
        {
            string errorName = DbusHelpers.PtrToString(dbus.dbus_message_get_error_name(message));
            return $"{typeName} sender='{sender}' dest='{dest}' path='{path}' iface='{iface}' member='{member}' sig='{signature}' error='{errorName}'";
        }

        return $"{typeName} sender='{sender}' dest='{dest}' path='{path}' iface='{iface}' member='{member}' sig='{signature}'";
    }

    private static string MessageTypeToString(int type)
    {
        return type switch
        {
            dbus.DBUS_MESSAGE_TYPE_METHOD_CALL => "method_call",
            dbus.DBUS_MESSAGE_TYPE_METHOD_RETURN => "method_return",
            dbus.DBUS_MESSAGE_TYPE_ERROR => "error",
            dbus.DBUS_MESSAGE_TYPE_SIGNAL => "signal",
            _ => $"type_{type}"
        };
    }

    private interface IObserver : IDisposable
    {
        bool IsMatch(DBusMessage* message);
        void Invoke(DBusMessage* message);
    }

    private sealed class Observer<T> : IObserver
    {
        private readonly Connection _connection;
        private readonly MatchRule _rule;
        private readonly MessageValueReader<T> _reader;
        private readonly Action<Exception?, T, object?> _handler;
        private readonly object? _readerState;
        private readonly object? _handlerState;
        private readonly SynchronizationContext? _context;
        private readonly string _matchString;
        private bool _disposed;

        public Observer(Connection connection, MatchRule rule, MessageValueReader<T> reader, Action<Exception?, T, object?> handler, object? readerState, object? handlerState, SynchronizationContext? context, string matchString)
        {
            _connection = connection;
            _rule = rule;
            _reader = reader;
            _handler = handler;
            _readerState = readerState;
            _handlerState = handlerState;
            _context = context;
            _matchString = matchString;
        }

        public bool IsMatch(DBusMessage* message)
        {
            if (_rule.Type != MessageType.Invalid && dbus.dbus_message_get_type(message) != (int)_rule.Type)
            {
                return false;
            }
            if (!MatchStringField(_rule.Sender, dbus.dbus_message_get_sender(message)))
            {
                return false;
            }
            if (!MatchStringField(_rule.Path, dbus.dbus_message_get_path(message)))
            {
                return false;
            }
            if (!MatchStringField(_rule.Interface, dbus.dbus_message_get_interface(message)))
            {
                return false;
            }
            if (!MatchStringField(_rule.Member, dbus.dbus_message_get_member(message)))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(_rule.Arg0))
            {
                if (!TryReadArg0(message, out var arg0) || !string.Equals(arg0, _rule.Arg0, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        public void Invoke(DBusMessage* message)
        {
            if (_disposed)
            {
                return;
            }

            void Dispatch()
            {
                try
                {
                    var msg = new Message(message);
                    var value = _reader(msg, _readerState);
                    _handler(null, value, _handlerState);
                }
                catch (Exception ex)
                {
                    _handler(ex, default!, _handlerState);
                }
            }

            if (_context == null)
            {
                Dispatch();
            }
            else
            {
                _context.Post(_ => Dispatch(), null);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            lock (_connection._gate)
            {
                _connection._observers.Remove(this);
            }
            if (!string.IsNullOrEmpty(_matchString))
            {
                DBusError error = default;
                dbus.dbus_error_init(&error);
                using var matchUtf8 = new Utf8String(_matchString);
                try
                {
                    DBusConnection* connection = _connection.BorrowConnection();
                    dbus.dbus_bus_remove_match(connection, matchUtf8.Pointer, &error);
                    ReleaseBorrowed(connection);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                if (dbus.dbus_error_is_set(&error) != 0)
                {
                    dbus.dbus_error_free(&error);
                }
            }
        }

        private static bool MatchStringField(string? expected, byte* actual)
        {
            if (string.IsNullOrEmpty(expected))
            {
                return true;
            }
            string actualStr = DbusHelpers.PtrToString(actual);
            return string.Equals(expected, actualStr, StringComparison.Ordinal);
        }

        private static bool TryReadArg0(DBusMessage* message, out string arg0)
        {
            arg0 = string.Empty;
            DBusMessageIter iter;
            if (dbus.dbus_message_iter_init(message, &iter) == 0)
            {
                return false;
            }
            if (dbus.dbus_message_iter_get_arg_type(&iter) != dbus.DBUS_TYPE_STRING)
            {
                return false;
            }
            byte* value;
            dbus.dbus_message_iter_get_basic(&iter, &value);
            arg0 = DbusHelpers.PtrToString(value);
            return true;
        }
    }

    private sealed class PathRegistration
    {
        public PathRegistration(Connection connection, IPathMethodHandler handler)
        {
            Connection = connection;
            Handler = handler;
        }

        public Connection Connection { get; }
        public IPathMethodHandler Handler { get; }
    }
}
