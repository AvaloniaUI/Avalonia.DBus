using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

public sealed class OrgFreedesktopDBusProxy(
    IDBusConnection connection,
    string destination,
    DBusObjectPath path,
    string iface)
{
    private const string DefaultInterface = "org.freedesktop.DBus";
    private readonly IDBusConnection _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    private readonly string _destination = destination ?? throw new ArgumentNullException(nameof(destination));
    private readonly string _interface = iface ?? throw new ArgumentNullException(nameof(iface));

    public OrgFreedesktopDBusProxy(
        IDBusConnection connection,
        string destination,
        DBusObjectPath path)
        : this(connection, destination, path, DefaultInterface)
    {
    }

    public async Task<uint> RequestNameAsync(
        string name,
        uint flags,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination,
            path,
            _interface,
            "RequestName",
            cancellationToken,
            name,
            flags);
        return reply.Body[0] switch
        {
            uint u => u,
            int i => unchecked((uint)i),
            _ => throw new InvalidOperationException("RequestName returned an unexpected value.")
        };
    }

    public async Task<uint> ReleaseNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination,
            path,
            _interface,
            "ReleaseName",
            cancellationToken,
            name);
        return reply.Body[0] switch
        {
            uint u => u,
            int i => unchecked((uint)i),
            _ => throw new InvalidOperationException("ReleaseName returned an unexpected value.")
        };
    }

    public async Task<string> GetNameOwnerAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination,
            path,
            _interface,
            "GetNameOwner",
            cancellationToken,
            name);
        if (reply.Body.Count == 0 || reply.Body[0] is not string owner)
            throw new InvalidOperationException("GetNameOwner returned an unexpected value.");
        return owner;
    }

    public Task<IDisposable> WatchNameOwnerChangedAsync(
        Action<string, string?, string?> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender,
            path,
            _interface,
            "NameOwnerChanged",
            message =>
            {
                var name = (string)message.Body[0];
                var oldOwner = NormalizeNameOwner((string)message.Body[1]);
                var newOwner = NormalizeNameOwner((string)message.Body[2]);
                handler(name, oldOwner, newOwner);
                return Task.CompletedTask;
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    public Task<IDisposable> WatchNameOwnerChangedAsync(
        Func<string, string?, string?, Task> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender,
            path,
            _interface,
            "NameOwnerChanged",
            message =>
            {
                var name = (string)message.Body[0];
                var oldOwner = NormalizeNameOwner((string)message.Body[1]);
                var newOwner = NormalizeNameOwner((string)message.Body[2]);
                return handler(name, oldOwner, newOwner);
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    private static string? NormalizeNameOwner(string owner)
    {
        return string.IsNullOrWhiteSpace(owner) ? null : owner;
    }
}

internal static class FreedesktopDBusGeneratedPrivateImplementationDoNotTouch
{
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Register()
    {
        DBusInteropMetadataRegistry.Register(
            new DBusInteropMetadata
            {
                ClrType = typeof(OrgFreedesktopDBusProxy),
                InterfaceName = "org.freedesktop.DBus",
                CreateProxy = static (connection, destination, path, iface) =>
                    new OrgFreedesktopDBusProxy(connection, destination, path, iface)
            });
    }
#pragma warning restore CA2255
}
