using System;
using System.Collections.Concurrent;
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

    private uint _nextSerial;
    private bool _disposed;

    public ChannelsDBusWireConnection(
        ChannelReader<DBusSerializedMessage> reader,
        ChannelWriter<DBusSerializedMessage> writer,
        string? uniqueName = null,
        bool isPeerToPeer = true)
    {
        _uniqueName = uniqueName;
        _isPeerToPeer = isPeerToPeer;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
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

    public Task<DBusMessage> SendWithReplyAsync(DBusMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        // Assign serial
        if (message.Serial == 0)
            message.Serial = Interlocked.Increment(ref _nextSerial);

        // Stamp sender
        message.Sender ??= _uniqueName;

        // Register pending reply
        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingReplies[message.Serial] = tcs;

        // Wire up cancellation
        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                if (_pendingReplies.TryRemove(message.Serial, out var removed))
                    removed.TrySetCanceled(cancellationToken);
            });
        }

        // Serialize and write to outbound channel
        var serialized = _serializer.Serialize(message);
        _outboundWriter.TryWrite(serialized);

        return tcs.Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the background receive loop
        _cts.Cancel();

        try
        {
            await _receiveLoopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Complete the outbound writer
        _outboundWriter.TryComplete();

        // Fail all pending replies
        var disposedEx = new ObjectDisposedException(nameof(ChannelsDBusWireConnection));
        foreach (var kvp in _pendingReplies)
        {
            if (_pendingReplies.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetException(disposedEx);
        }

        // Complete the internal receiving channel
        _receiving.Writer.TryComplete();

        _cts.Dispose();
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
                catch
                {
                    // Skip malformed messages
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
