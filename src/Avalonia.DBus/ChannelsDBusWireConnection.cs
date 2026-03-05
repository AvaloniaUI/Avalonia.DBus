using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Managed;

namespace Avalonia.DBus;

/// <summary>
/// An implementation of <see cref="IDBusWireConnection"/> backed by
/// <see cref="Channel{T}"/> pairs of <see cref="DBusSerializedMessage"/>,
/// with reply correlation and serial assignment.
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

    private uint _nextSerial;
    private int _disposed;

    public ChannelsDBusWireConnection(
        ChannelReader<DBusSerializedMessage> reader,
        ChannelWriter<DBusSerializedMessage> writer,
        Socket? socket = null,
        CancellationTokenSource? cts = null,
        string? uniqueName = null,
        bool isPeerToPeer = true)
    {
        _uniqueName = uniqueName;
        _isPeerToPeer = isPeerToPeer;
        _socket = socket;
        _transportCts = cts;
        _outboundWriter = writer;
        _serializer = new ManagedDBusMessageSerializer();
        _receiving = Channel.CreateUnbounded<DBusMessage>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        _receiveLoopTask = RunReceiveLoopAsync(reader, _cts.Token);
    }

    public bool IsPeerToPeer => _isPeerToPeer;

    public ChannelReader<DBusMessage> ReceivingReader => _receiving.Reader;

    public Task<string?> GetUniqueNameAsync() => Task.FromResult(_uniqueName);

    public Task SendAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Assign serial if not set
        if (message.Serial == 0)
            message.Serial = Interlocked.Increment(ref _nextSerial);

        // Stamp sender
        message.Sender ??= _uniqueName;

        // Serialize and write to outbound channel
        var serialized = _serializer.Serialize(message);
        return _outboundWriter.WriteAsync(serialized, cancellationToken).AsTask();
    }

    public async Task<DBusMessage> SendWithReplyAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Assign serial
        if (message.Serial == 0)
            message.Serial = Interlocked.Increment(ref _nextSerial);

        // Stamp sender
        message.Sender ??= _uniqueName;

        // Register pending reply
        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[message.Serial] = tcs;

        // Wire up cancellation with proper registration lifetime (F4)
        CancellationTokenRegistration reg = default;
        if (cancellationToken.CanBeCanceled)
        {
            reg = cancellationToken.Register(() =>
            {
                if (_pendingReplies.TryRemove(message.Serial, out var removed))
                    removed.TrySetCanceled(cancellationToken);
            });

            // Dispose registration when TCS completes (prevents leak for long-lived tokens)
            _ = tcs.Task.ContinueWith(_ => reg.Dispose(), TaskContinuationOptions.ExecuteSynchronously);
        }

        // Serialize and write to outbound channel (F1: use WriteAsync, not TryWrite)
        var serialized = _serializer.Serialize(message);
        try
        {
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

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        // 1. Signal transport background tasks to stop
        _transportCts?.Cancel();

        // 2. Cancel our own receive loop
        _cts.Cancel();

        try
        {
            await _receiveLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // 3. Complete the outbound writer
        _outboundWriter.TryComplete();

        // 4. Close socket (causes background reader/writer to exit via SocketException)
        _socket?.Dispose();

        // 5. Dispose CTS resources
        _transportCts?.Dispose();
        _cts.Dispose();

        // 6. Fail all pending replies
        var disposedEx = new ObjectDisposedException(nameof(ChannelsDBusWireConnection));
        foreach (var kvp in _pendingReplies)
        {
            if (_pendingReplies.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetException(disposedEx);
        }

        // 7. Complete the internal receiving channel
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
                    // TODO: Replace Debug.WriteLine with a unified logging system
                    Debug.WriteLine($"Skipping malformed D-Bus message: {ex.Message}");
                    continue;
                }

                // Check if this is a reply to a pending request
                if (message.Type is DBusMessageType.MethodReturn or DBusMessageType.Error
                    && message.ReplySerial != 0
                    && _pendingReplies.TryRemove(message.ReplySerial, out var pendingTcs))
                {
                    pendingTcs.TrySetResult(message);
                }
                else
                {
                    // Non-reply message: deliver to the receiving channel
                    await _receiving.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
                }
            }
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
}
