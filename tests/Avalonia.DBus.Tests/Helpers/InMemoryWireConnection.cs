using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// Paired in-memory transport for testing. <see cref="CreatePair"/> returns two connected
/// instances where messages sent by one arrive at the other.
/// When a <see cref="IDBusMessageSerializer"/> is provided, every message is serialized and
/// deserialized before delivery — simulating a real wire and exercising the full marshal path.
/// </summary>
internal sealed class InMemoryWireConnection : IDBusWireConnection
{
    private readonly string? _uniqueName;
    private readonly IDBusMessageSerializer? _serializer;
    private readonly Channel<DBusMessage> _receiving;
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<DBusMessage>> _pendingReplies = new();

    private InMemoryWireConnection? _partner;
    private uint _nextSerial;
    private bool _disposed;

    private InMemoryWireConnection(string? uniqueName, IDBusMessageSerializer? serializer)
    {
        _uniqueName = uniqueName;
        _serializer = serializer;
        _receiving = Channel.CreateUnbounded<DBusMessage>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    }

    /// <summary>
    /// Creates a cross-linked pair of in-memory wire connections.
    /// When <paramref name="serializer"/> is provided, messages are round-tripped through
    /// serialize/deserialize on every send — turning every test into a marshaller test.
    /// </summary>
    public static (InMemoryWireConnection A, InMemoryWireConnection B) CreatePair(
        string? nameA = ":mem.A",
        string? nameB = ":mem.B",
        IDBusMessageSerializer? serializer = null)
    {
        var a = new InMemoryWireConnection(nameA, serializer);
        var b = new InMemoryWireConnection(nameB, serializer);
        a._partner = b;
        b._partner = a;
        return (a, b);
    }

    public bool IsPeerToPeer => true;

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

        // If this is a reply, check if the partner has a pending reply TCS for this serial
        if (message.Type is DBusMessageType.MethodReturn or DBusMessageType.Error
            && _partner != null
            && _partner._pendingReplies.TryRemove(message.ReplySerial, out var pendingTcs))
        {
            pendingTcs.TrySetResult(Marshal(message));
            return Task.CompletedTask;
        }

        // Otherwise, deliver to partner's receiving channel
        _partner?._receiving.Writer.TryWrite(Marshal(message));
        return Task.CompletedTask;
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

        // Deliver to partner's receiving channel (marshal the outgoing call)
        _partner?._receiving.Writer.TryWrite(Marshal(message));

        return tcs.Task;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        // Fail all pending replies
        var disposedEx = new ObjectDisposedException(nameof(InMemoryWireConnection));
        foreach (var kvp in _pendingReplies)
        {
            if (_pendingReplies.TryRemove(kvp.Key, out var tcs))
                tcs.TrySetException(disposedEx);
        }

        _receiving.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Round-trips a message through the serializer when one is configured.
    /// This ensures every message exercises the full serialized -> deserialized path.
    /// </summary>
    private DBusMessage Marshal(DBusMessage message)
    {
        if (_serializer == null)
            return message;

        using var stream = new MemoryStream();
        _serializer.Serialize(message, stream);
        stream.Position = 0;
        return _serializer.Deserialize(stream);
    }
}
