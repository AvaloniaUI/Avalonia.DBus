using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus.Wire;

internal sealed class DBusWireReceiver
{
    private readonly DBusWireConnection _owner;
    private Socket? _socket;
    private readonly byte[] _peekBuffer = new byte[1];
    private static readonly TimeSpan s_idleDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan s_socketWait = TimeSpan.FromMilliseconds(250);

    public DBusWireReceiver(DBusWireConnection owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _socket = owner.IoSocket;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_owner.TryGetConnection(out var connectionPtr))
            {
                return;
            }

            int processed;
            try
            {
                await _owner.WaitIoGateAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                processed = _owner.DrainMessagesUnsafe(connectionPtr);
            }
            finally
            {
                _owner.ReleaseIoGate();
            }

            if (processed > 0)
            {
                continue;
            }

            var socket = _socket;
            if (socket == null)
            {
                try
                {
                    await Task.Delay(s_idleDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(s_socketWait);
                int received = await socket.ReceiveAsync(_peekBuffer, SocketFlags.Peek, timeoutCts.Token).ConfigureAwait(false);
                if (received == 0)
                {
                    socket.Dispose();
                    _socket = null;
                    continue;
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                _socket = null;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
            {
                try
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (SocketException)
            {
                socket.Dispose();
                _socket = null;
            }
        }
    }
}
