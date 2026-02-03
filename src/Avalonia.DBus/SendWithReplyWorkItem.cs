using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus.Wire;

public sealed unsafe partial class DBusWireConnection
{

    private sealed partial class SendWorkItem
    {
        private int _canceled;
        private long _serial;

        public SendWorkItem(DBusWireConnection connection,
            DBusMessage message,
            TaskCompletionSource<DBusMessage> completion,
            CancellationToken cancellationToken,
            DateTime startTimestamp, bool expectingReply = false)
        {
            Message = message;
            CancellationToken = cancellationToken;
            Connection = connection;
            Completion = completion;
            StartTimestamp = startTimestamp;
            ExpectingReply = expectingReply;
        }

        public DBusWireConnection Connection { get; set; }

        public TaskCompletionSource<DBusMessage> Completion { get; }
        public DateTime StartTimestamp { get; }
        public bool ExpectingReply { get; }

        public void Cancel()
            => Completion.TrySetCanceled(CancellationToken);

        public bool TryCancel()
        {
            if (Interlocked.Exchange(ref _canceled, 1) != 0)
            {
                return false;
            }

            Completion.TrySetCanceled(CancellationToken);
            return true;
        }

        public void Fail(Exception exception)
            => Completion.TrySetException(exception);

        public DBusMessage Message { get; }

        public CancellationToken CancellationToken { get; }

        public bool IsCanceled => Volatile.Read(ref _canceled) != 0;

        public uint Serial
        {
            get => (uint)Volatile.Read(ref _serial);
            set => Volatile.Write(ref _serial, value);
        }
    }

    private struct WatchState(int fd, PollEvents events, bool enabled)
    {
        public int Fd { get; set; } = fd;
        public PollEvents Events { get; set; } = events;
        public bool Enabled { get; set; } = enabled;
    }
}
