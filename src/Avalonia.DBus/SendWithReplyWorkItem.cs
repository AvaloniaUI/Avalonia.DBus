using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus.Wire;

public sealed unsafe partial class DBusWireConnection
{

    private sealed partial class SendWithReplyWorkItem
    {
        public SendWithReplyWorkItem(DBusWireConnection connection,
            DBusMessage message,
            TaskCompletionSource<DBusMessage> completion,
            CancellationToken cancellationToken,
            DateTime startTimestamp)
        {
            Message = message;
            CancellationToken = cancellationToken;
            Connection = connection;
            Completion = completion;
            StartTimestamp = startTimestamp;
        }

        public DBusWireConnection Connection { get; set; }

        public TaskCompletionSource<DBusMessage> Completion { get; }
        public DateTime StartTimestamp { get; }

        public void Cancel()
            => Completion.TrySetCanceled(CancellationToken);

        public void Fail(Exception exception)
            => Completion.TrySetException(exception);

        public DBusMessage Message { get; }

        public CancellationToken CancellationToken { get; }
    }

    private struct WatchState(int fd, PollEvents events, bool enabled)
    {
        public int Fd { get; set; } = fd;
        public PollEvents Events { get; set; } = events;
        public bool Enabled { get; set; } = enabled;
    }
}