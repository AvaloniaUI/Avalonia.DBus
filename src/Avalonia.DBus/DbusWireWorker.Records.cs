using System;
using System.Threading;
using System.Threading.Tasks;

using DBusNativeMessagePtr = System.IntPtr;
using DBusWatchPtr = System.IntPtr;

namespace Avalonia.DBus.Wire;

internal sealed partial class DbusWireWorker
{
    
    internal abstract record WireWorkerMessage;

    internal sealed record DisposeMessage : WireWorkerMessage;

    internal sealed record FetchUniqueNameMessage(TaskCompletionSource<string?> ReturnTcs) : WireWorkerMessage;

    internal sealed record EnqueueSendItemMessage(
        DBusMessage Message,
        TaskCompletionSource<DBusMessage> Completion,
        bool ExpectingReply = false,
        CancellationToken CancellationToken = default) : WireWorkerMessage;

    internal sealed record EnqueueHandleCallbackMessage(DBusNativeMessagePtr MsgPtr) : WireWorkerMessage;

    internal sealed record AddWatchMessage(DBusWatchPtr WatchPtr) : WireWorkerMessage;

    internal sealed record ToggleWatchMessage(DBusWatchPtr WatchPtr) : WireWorkerMessage;

    internal sealed record RemoveWatchMessage(DBusWatchPtr WatchPtr) : WireWorkerMessage;

    private sealed record CancelSendItemMessage(SendWorkItem WorkItem) : WireWorkerMessage;
    
    private record WatchState(int Fd, PollEvents Events, bool Enabled);
    private record CancelRegistrationState(DbusWireWorker Worker, SendWorkItem WorkItem);

    private record SendWorkItem(
        DBusMessage Message,
        TaskCompletionSource<DBusMessage> Completion,
        DateTime StartTimestamp,
        bool ExpectingReply = false,
        CancellationToken CancellationToken = default
    )
    {
        private int _canceled;
        private long _serial;

        public TaskCompletionSource<DBusMessage> Completion { get; } = Completion;
        public DateTime StartTimestamp { get; } = StartTimestamp;
        public bool ExpectingReply { get; } = ExpectingReply;

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

        public DBusMessage Message { get; } = Message;

        public CancellationToken CancellationToken { get; } = CancellationToken;

        public bool IsCanceled => Volatile.Read(ref _canceled) != 0;

        public uint Serial
        {
            get => (uint)Volatile.Read(ref _serial);
            set => Volatile.Write(ref _serial, value);
        }
    }
}