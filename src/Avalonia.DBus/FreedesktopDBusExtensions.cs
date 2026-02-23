using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

#if !AVDBUS_INTERNAL
public
#endif
static class FreedesktopDBusExtensions
{
    private const string BusDestination = "org.freedesktop.DBus";
    private static readonly DBusObjectPath BusPath = "/org/freedesktop/DBus";

    extension(IDBusConnection connection)
    {
        internal OrgFreedesktopDBusProxy CreateFreedesktopDBusProxy()
        {
            ArgumentNullException.ThrowIfNull(connection);
            return new OrgFreedesktopDBusProxy(connection, BusDestination, BusPath);
        }

        public async Task<DBusRequestNameReply> RequestNameAsync(string name,
            DBusRequestNameFlags flags = DBusRequestNameFlags.None,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name is required.", nameof(name));

            var value = await connection
                .CreateFreedesktopDBusProxy()
                .RequestNameAsync(name, (uint)flags, cancellationToken);

            return (DBusRequestNameReply)value;
        }

        public async Task ReleaseNameAsync(string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name is required.", nameof(name));

            _ = await connection
                .CreateFreedesktopDBusProxy()
                .ReleaseNameAsync(name, cancellationToken);
        }

        public async Task<string?> GetNameOwnerAsync(string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name is required.", nameof(name));

            try
            {
                var owner = await connection
                    .CreateFreedesktopDBusProxy()
                    .GetNameOwnerAsync(name, cancellationToken);
                return string.IsNullOrWhiteSpace(owner) ? null : owner;
            }
            catch (DBusException ex) when (string.Equals(ex.ErrorName, "org.freedesktop.DBus.Error.NameHasNoOwner", StringComparison.Ordinal))
            {
                return null;
            }
        }

        public async Task<bool> NameHasOwnerAsync(string name,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Name is required.", nameof(name));

            return await connection
                .CreateFreedesktopDBusProxy()
                .NameHasOwnerAsync(name, cancellationToken);
        }

        public async Task<List<string>> ListNamesAsync(
            CancellationToken cancellationToken = default)
        {
            return await connection
                .CreateFreedesktopDBusProxy()
                .ListNamesAsync(cancellationToken);
        }

        public async Task<string> GetIdAsync(
            CancellationToken cancellationToken = default)
        {
            return await connection
                .CreateFreedesktopDBusProxy()
                .GetIdAsync(cancellationToken);
        }

        public Task<IDisposable> WatchNameOwnerChangedAsync(Action<string, string?, string?> handler,
            bool emitOnCapturedContext = true,
            string? sender = null)
        {
            ArgumentNullException.ThrowIfNull(connection);
            return connection.CreateFreedesktopDBusProxy()
                .WatchNameOwnerChangedAsync(handler, sender, emitOnCapturedContext);
        }

        public Task<IDisposable> WatchNameOwnerChangedAsync(Func<string, string?, string?, Task> handler,
            bool emitOnCapturedContext = true,
            string? sender = null)
        {
            ArgumentNullException.ThrowIfNull(connection);
            return connection.CreateFreedesktopDBusProxy()
                .WatchNameOwnerChangedAsync(handler, sender, emitOnCapturedContext);
        }
    }
}
