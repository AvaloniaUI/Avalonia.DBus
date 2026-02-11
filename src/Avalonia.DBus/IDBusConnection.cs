using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public interface IDBusConnection : IAsyncDisposable
{
    object CreateProxy(
        Type interfaceType,
        string destination,
        DBusObjectPath path,
        string? iface = null);

    Task SendMessageAsync(
        DBusMessage message,
        CancellationToken cancellationToken = default);

    Task<DBusMessage> CallMethodAsync(
        string destination,
        DBusObjectPath path,
        string iface,
        string member,
        CancellationToken cancellationToken = default,
        params object[] args);

    Task<IDisposable> SubscribeAsync(
        string? sender,
        DBusObjectPath? path,
        string iface,
        string member,
        Func<DBusMessage, Task> handler,
        SynchronizationContext? synchronizationContext = null);
}
