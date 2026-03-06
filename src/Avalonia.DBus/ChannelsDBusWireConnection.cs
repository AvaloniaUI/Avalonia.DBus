using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Managed;

namespace Avalonia.DBus;

/// <summary>
/// An <see cref="IDBusWireConnection"/> implementation that exchanges
/// <see cref="DBusSerializedMessage"/> values through channels, assigns
/// request serials, and correlates replies on a background receive loop.
/// </summary>
#if !AVDBUS_INTERNAL
public
#endif
sealed class ChannelsDBusWireConnection : IDBusWireConnection
{
    private readonly string? _uniqueName;
    private readonly bool _isPeerToPeer;
    private readonly IDBusMessageSerializer _serializer;
    private readonly ChannelWriter<DBusSerializedMessage> _outboundWriter;
    private readonly Channel<DBusMessage> _receiving;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<DBusMessage>> _pendingReplies = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiveLoopTask;
    private readonly Socket? _socket;
    private readonly CancellationTokenSource? _transportCts;
    private readonly IDBusDiagnostics? _diagnostics;

    private uint _nextSerial;
    private int _disposed;

    /// <summary>
    /// Creates a channel-backed wire connection.
    /// </summary>
    /// <param name="reader">Produces serialized inbound messages.</param>
    /// <param name="writer">Consumes serialized outbound messages.</param>
    /// <param name="socket">An optional socket to dispose with the connection.</param>
    /// <param name="cts">An optional cancellation source controlling the underlying transport tasks.</param>
    /// <param name="uniqueName">
    /// The bus-assigned unique name for this connection, or <see langword="null"/> for peer-to-peer transports.
    /// </param>
    /// <param name="isPeerToPeer"><see langword="true"/> when the connection is not attached to a message bus.</param>
    public ChannelsDBusWireConnection(
        ChannelReader<DBusSerializedMessage> reader,
        ChannelWriter<DBusSerializedMessage> writer,
        Socket? socket = null,
        CancellationTokenSource? cts = null,
        string? uniqueName = null,
        bool isPeerToPeer = true)
        : this(reader, writer, socket, cts, uniqueName, isPeerToPeer, diagnostics: null, serializer: null)
    {
    }

    /// <summary>
    /// Creates a channel-backed wire connection with diagnostics hooks.
    /// </summary>
    /// <param name="reader">Produces serialized inbound messages.</param>
    /// <param name="writer">Consumes serialized outbound messages.</param>
    /// <param name="socket">An optional socket to dispose with the connection.</param>
    /// <param name="cts">An optional cancellation source controlling the underlying transport tasks.</param>
    /// <param name="uniqueName">
    /// The bus-assigned unique name for this connection, or <see langword="null"/> for peer-to-peer transports.
    /// </param>
    /// <param name="isPeerToPeer"><see langword="true"/> when the connection is not attached to a message bus.</param>
    /// <param name="diagnostics">Receives transport warnings such as malformed inbound messages.</param>
    public ChannelsDBusWireConnection(
        ChannelReader<DBusSerializedMessage> reader,
        ChannelWriter<DBusSerializedMessage> writer,
        Socket? socket,
        CancellationTokenSource? cts,
        string? uniqueName,
        bool isPeerToPeer,
        IDBusDiagnostics? diagnostics)
        : this(reader, writer, socket, cts, uniqueName, isPeerToPeer, diagnostics, serializer: null)
    {
    }

    internal ChannelsDBusWireConnection(
        ChannelReader<DBusSerializedMessage> reader,
        ChannelWriter<DBusSerializedMessage> writer,
        Socket? socket,
        CancellationTokenSource? cts,
        string? uniqueName,
        bool isPeerToPeer,
        IDBusDiagnostics? diagnostics,
        IDBusMessageSerializer? serializer)
    {
        _uniqueName = uniqueName;
        _isPeerToPeer = isPeerToPeer;
        _socket = socket;
        _transportCts = cts;
        _outboundWriter = writer;
        _diagnostics = diagnostics;
        _serializer = serializer ?? new ManagedDBusMessageSerializer();
        _receiving = Channel.CreateUnbounded<DBusMessage>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        _receiveLoopTask = RunReceiveLoopAsync(reader, _cts.Token);
    }

    /// <summary>
    /// Gets a value indicating whether this connection is peer-to-peer and therefore has no message bus.
    /// </summary>
    public bool IsPeerToPeer => _isPeerToPeer;

    /// <summary>
    /// Gets the reader for inbound messages that were not matched to a pending reply.
    /// </summary>
    public ChannelReader<DBusMessage> ReceivingReader => _receiving.Reader;

    /// <summary>
    /// Returns the bus-assigned unique name supplied at construction time, or <see langword="null"/> for peer-to-peer transports.
    /// </summary>
    public Task<string?> GetUniqueNameAsync() => Task.FromResult(_uniqueName);

    /// <summary>
    /// Serializes and sends a message without waiting for a reply.
    /// </summary>
    public Task SendAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (message.Serial == 0)
            message.Serial = GetNextSerial();

        message.Sender ??= _uniqueName;

        var serialized = _serializer.Serialize(message);
        return _outboundWriter.WriteAsync(serialized, cancellationToken).AsTask();
    }

    /// <summary>
    /// Serializes and sends a message, then waits for a reply whose reply serial matches the request serial.
    /// </summary>
    public async Task<DBusMessage> SendWithReplyAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (message.Serial == 0)
            message.Serial = GetNextSerial();

        message.Sender ??= _uniqueName;

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[message.Serial] = tcs;

        // Remove canceled waits from the pending-reply table.
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(() =>
            {
                if (_pendingReplies.TryRemove(message.Serial, out var removed))
                    removed.TrySetCanceled(cancellationToken);
            });

            // Dispose the registration when the wait completes to avoid leaking long-lived tokens.
            _ = tcs.Task.ContinueWith(_ => reg.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        }

        try
        {
            var serialized = _serializer.Serialize(message);
            await _outboundWriter.WriteAsync(serialized, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _pendingReplies.TryRemove(message.Serial, out _);
            reg.Dispose();
            throw;
        }

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Stops the receive loop, shuts down the underlying transport, and fails outstanding reply waiters.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _transportCts?.Cancel();

        _cts.Cancel();

        try
        {
            await _receiveLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _outboundWriter.TryComplete();

        _socket?.Dispose();

        _transportCts?.Dispose();
        _cts.Dispose();

        // Fail outstanding reply waiters after transport shutdown.
        var disposedEx = new ObjectDisposedException(nameof(ChannelsDBusWireConnection));
        foreach (var kvp in _pendingReplies)
        {
            if (_pendingReplies.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetException(disposedEx);
        }

        _receiving.Writer.TryComplete();
    }

    private async Task RunReceiveLoopAsync(ChannelReader<DBusSerializedMessage> reader, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var serialized in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                DBusMessage message;
                try
                {
                    message = _serializer.Deserialize(serialized);
                }
                catch (Exception ex) when (ex is InvalidDataException
                    or NotSupportedException
                    or InvalidOperationException
                    or FormatException)
                {
                    DBusTransportLog.MalformedMessageSkipped(_diagnostics, ex);
                    continue;
                }

                if (message.Type is DBusMessageType.MethodReturn or DBusMessageType.Error
                    && message.ReplySerial != 0
                    && _pendingReplies.TryRemove(message.ReplySerial, out var pendingTcs))
                {
                    pendingTcs.TrySetResult(message);
                }
                else
                {
                    await _receiving.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
                }
            }

            if (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _disposed) == 0)
                DBusTransportLog.InboundTransportCompleted(_diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // The inbound channel was completed — normal shutdown
        }
    }

    private uint GetNextSerial()
    {
        uint serial;
        do
        {
            serial = Interlocked.Increment(ref _nextSerial);
        } while (serial == 0);

        return serial;
    }
}
