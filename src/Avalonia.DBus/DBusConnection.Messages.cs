using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
sealed partial class DBusConnection
{
    private sealed record MethodCallMessage(
        DBusMessage Call,
        TaskCompletionSource<DBusMessage> ReplyTcs,
        CancellationToken CancellationToken);

    private sealed record RegisterObjectsMessage(
        DBusObjectPath Path,
        IReadOnlyList<object> Targets,
        SynchronizationContext? SynchronizationContext,
        object Token,
        TaskCompletionSource<bool> Completion);

    private sealed record UnRegisterObjectsMessage(object Token);

    private sealed record SubscribeMessage(
        string? Sender,
        DBusObjectPath? Path,
        string Interface,
        string Member,
        Func<DBusMessage, Task> Handler,
        SynchronizationContext? SynchronizationContext,
        object Token,
        TaskCompletionSource<bool> Completion);

    private sealed record UnsubscribeMessage(object Token);

    private sealed record RawDBusMessageMessage(
        DBusMessage Message,
        CancellationToken CancellationToken,
        TaskCompletionSource<bool>? Completion);

    private sealed record GetUniqueNameMessage(TaskCompletionSource<string?> Completion);

    private sealed record DisposeConnectionMessage(TaskCompletionSource<bool> Completion);

    private sealed record IncomingWireMessage(DBusMessage Message);
}
