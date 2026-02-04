using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Avalonia.DBus;

internal sealed class AsyncMessageQueue
{
    private readonly ConcurrentQueue<DBusMessage> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private volatile bool _completed;

    public void Enqueue(DBusMessage message)
    {
        if (_completed)
        {
            return;
        }

        _queue.Enqueue(message);
        _signal.Release();
    }

    public void Complete()
    {
        _completed = true;
        _signal.Release();
    }

    public async IAsyncEnumerable<DBusMessage> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            await _signal.WaitAsync(cancellationToken);

            while (_queue.TryDequeue(out var message))
            {
                yield return message;
            }

            if (_completed)
            {
                yield break;
            }
        }
    }
}
