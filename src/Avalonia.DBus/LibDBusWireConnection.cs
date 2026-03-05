using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.DBus.Native;

namespace Avalonia.DBus;

/// <summary>
/// Low-level connection handling raw DBus message transport.
/// </summary>
internal sealed class LibDBusWireConnection : IDBusWireConnection
{
    private readonly DbusWireWorker _worker;

    private LibDBusWireConnection(DbusWireWorker worker)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
    }

    /// <summary>
    /// Connects to a D-Bus bus at the specified address.
    /// </summary>
    public static Task<LibDBusWireConnection> ConnectAsync(
        string address,
        CancellationToken cancellationToken = default)
        => ConnectAsync(address, diagnostics: null, cancellationToken);

    internal static Task<LibDBusWireConnection> ConnectAsync(
        string address,
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        if (string.Equals(address, "session", StringComparison.OrdinalIgnoreCase))
            return ConnectSessionAsync(diagnostics, cancellationToken);

        if (string.Equals(address, "system", StringComparison.OrdinalIgnoreCase))
            return ConnectSystemAsync(diagnostics, cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        var worker = DbusWireWorker.OpenAddress(address, diagnostics);
        return Task.FromResult(new LibDBusWireConnection(worker));
    }

    /// <summary>
    /// Connects to the session bus.
    /// </summary>
    public static Task<LibDBusWireConnection> ConnectSessionAsync(
        CancellationToken cancellationToken = default)
        => ConnectSessionAsync(diagnostics: null, cancellationToken);

    internal static Task<LibDBusWireConnection> ConnectSessionAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var worker = DbusWireWorker.OpenBus(DBusBusType.DBUS_BUS_SESSION, diagnostics);
        return Task.FromResult(new LibDBusWireConnection(worker));
    }

    /// <summary>
    /// Connects to the system bus.
    /// </summary>
    public static Task<LibDBusWireConnection> ConnectSystemAsync(
        CancellationToken cancellationToken = default)
        => ConnectSystemAsync(diagnostics: null, cancellationToken);

    internal static Task<LibDBusWireConnection> ConnectSystemAsync(
        IDBusDiagnostics? diagnostics,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var worker = DbusWireWorker.OpenBus(DBusBusType.DBUS_BUS_SYSTEM, diagnostics);
        return Task.FromResult(new LibDBusWireConnection(worker));
    }

    /// <summary>
    /// The unique name assigned by the message bus (e.g., ":1.42").
    /// Null if not connected to a message bus.
    /// </summary>
    public Task<string?> GetUniqueNameAsync()
    {
        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _worker.TryEnqueue(new DbusWireWorker.FetchUniqueNameMessage(tcs));
        return tcs.Task;
    }

    /// <summary>
    /// Sends a message without waiting for a reply.
    /// </summary>
    public async Task SendAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _worker.TryEnqueue(new DbusWireWorker.EnqueueSendItemMessage(
            message,
            tcs,
            false,
            cancellationToken));

        await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message and waits for a reply.
    /// </summary>
    public async Task<DBusMessage> SendWithReplyAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var tcs = new TaskCompletionSource<DBusMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        _worker.TryEnqueue(new DbusWireWorker.EnqueueSendItemMessage(
            message,
            tcs,
            true,
            cancellationToken
            ));

        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Reader for incoming messages (METHOD_CALL, SIGNAL, etc.).
    /// Used by higher-level connection workers.
    /// </summary>
    public ChannelReader<DBusMessage> ReceivingReader => _worker.ReceivingReader;

    /// <summary>
    /// Closes the connection and releases resources.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        _worker.TryEnqueue(new DbusWireWorker.DisposeMessage());
        return new ValueTask(_worker.DisposeTask);
    }
}
