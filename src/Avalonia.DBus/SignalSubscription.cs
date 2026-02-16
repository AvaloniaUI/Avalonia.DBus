using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

internal sealed class SignalSubscription(
    object gate,
    List<SignalSubscription> subscriptions,
    Action<string> removeMatch,
    Action<Task> fireAndForget,
    string? sender,
    DBusObjectPath? path,
    string iface,
    string member,
    Func<DBusMessage, Task> handler,
    SynchronizationContext? context,
    string matchRule,
    IDBusDiagnostics? diagnostics)
    : IDisposable
{
    private bool _disposed;

    public bool IsMatch(DBusMessage message)
    {
        if (message.Type != DBusMessageType.Signal)
            return false;

        if (!string.IsNullOrEmpty(sender) && !string.Equals(message.Sender, sender, StringComparison.Ordinal))
            return false;

        if (path.HasValue)
        {
            if (!message.Path.HasValue)
                return false;

            if (message.Path.Value != path.Value)
                return false;
        }

        if (!string.Equals(message.Interface, iface, StringComparison.Ordinal))
            return false;

        if (!string.Equals(message.Member, member, StringComparison.Ordinal))
            return false;

        return true;
    }

    public void Invoke(DBusMessage message)
    {
        if (_disposed)
            return;

        if (context == null)
            fireAndForget(InvokeHandlerAsync(message));
        else
            context.Post(_ => fireAndForget(InvokeHandlerAsync(message)), null);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        lock (gate)
        {
            subscriptions.Remove(this);
        }

        removeMatch(matchRule);
    }

    private async Task InvokeHandlerAsync(DBusMessage message)
    {
        try
        {
            await handler(message);
        }
        catch (Exception ex)
        {
            diagnostics?.OnUnobservedException(ex);
        }
    }
}
