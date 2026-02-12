using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

internal sealed class ObjectHandlerRegistration(
    object gate,
    Dictionary<ObjectHandlerKey, ObjectHandlerRegistration> handlers,
    Func<DBusMessage, DBusMessage, DBusMessage> ensureReplyMetadata,
    Func<DBusMessage, Task> sendReplyAsync,
    Action<Task> fireAndForget,
    IDBusConnection connection,
    ObjectHandlerKey key,
    IDBusInterfaceCallDispatcher handler,
    SynchronizationContext? context)
    : IDisposable
{
    private bool _disposed;

    public void Invoke(DBusMessage message)
    {
        if (_disposed)
            return;

        if (context == null)
        {
            fireAndForget(HandleAsync(message));
        }
        else
        {
            context.Post(_ => fireAndForget(HandleAsync(message)), null);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (gate)
        {
            handlers.Remove(key);
        }
    }

    private async Task HandleAsync(DBusMessage message)
    {
        DBusMessage reply;
        try
        {
            reply = await handler.Handle(connection, message);
            if (reply == null)
                reply = message.CreateError("org.freedesktop.DBus.Error.Failed", "Handler returned null reply.");
        }
        catch (Exception ex)
        {
            reply = message.CreateError("org.freedesktop.DBus.Error.Failed", ex.Message);
        }

        reply = ensureReplyMetadata(message, reply);
        await sendReplyAsync(reply);
    }
}
