using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus;

internal sealed class OrgFreedesktopDBusProxy(
    IDBusConnection connection,
    string destination,
    DBusObjectPath path,
    string iface)
{
    private const string DefaultInterface = "org.freedesktop.DBus";
    private const string PropertiesInterface = "org.freedesktop.DBus.Properties";
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

    // --- Methods ---

    public async Task<string> HelloAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "Hello", cancellationToken);
        return (string)reply.Body[0];
    }

    public async Task<uint> RequestNameAsync(
        string name,
        uint flags,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "RequestName", cancellationToken, name, flags);
        return CastUInt32(reply.Body[0]);
    }

    public async Task<uint> ReleaseNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "ReleaseName", cancellationToken, name);
        return CastUInt32(reply.Body[0]);
    }

    public async Task<uint> StartServiceByNameAsync(
        string name,
        uint flags,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "StartServiceByName", cancellationToken, name, flags);
        return CastUInt32(reply.Body[0]);
    }

    public async Task UpdateActivationEnvironmentAsync(
        Dictionary<string, string> environment,
        CancellationToken cancellationToken = default)
    {
        await _connection.CallMethodAsync(
            _destination, path, _interface, "UpdateActivationEnvironment", cancellationToken, environment);
    }

    public async Task<bool> NameHasOwnerAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "NameHasOwner", cancellationToken, name);
        return (bool)reply.Body[0];
    }

    public async Task<List<string>> ListNamesAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "ListNames", cancellationToken);
        return (List<string>)reply.Body[0];
    }

    public async Task<List<string>> ListActivatableNamesAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "ListActivatableNames", cancellationToken);
        return (List<string>)reply.Body[0];
    }

    public async Task AddMatchAsync(
        string rule,
        CancellationToken cancellationToken = default)
    {
        await _connection.CallMethodAsync(
            _destination, path, _interface, "AddMatch", cancellationToken, rule);
    }

    public async Task RemoveMatchAsync(
        string rule,
        CancellationToken cancellationToken = default)
    {
        await _connection.CallMethodAsync(
            _destination, path, _interface, "RemoveMatch", cancellationToken, rule);
    }

    public async Task<string> GetNameOwnerAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetNameOwner", cancellationToken, name);
        if (reply.Body.Count == 0 || reply.Body[0] is not string owner)
            throw new InvalidOperationException("GetNameOwner returned an unexpected value.");
        return owner;
    }

    public async Task<List<string>> ListQueuedOwnersAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "ListQueuedOwners", cancellationToken, name);
        return (List<string>)reply.Body[0];
    }

    public async Task<uint> GetConnectionUnixUserAsync(
        string busName,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetConnectionUnixUser", cancellationToken, busName);
        return CastUInt32(reply.Body[0]);
    }

    public async Task<uint> GetConnectionUnixProcessIDAsync(
        string busName,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetConnectionUnixProcessID", cancellationToken, busName);
        return CastUInt32(reply.Body[0]);
    }

    public async Task<List<byte>> GetAdtAuditSessionDataAsync(
        string busName,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetAdtAuditSessionData", cancellationToken, busName);
        return (List<byte>)reply.Body[0];
    }

    public async Task<List<byte>> GetConnectionSELinuxSecurityContextAsync(
        string busName,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetConnectionSELinuxSecurityContext", cancellationToken, busName);
        return (List<byte>)reply.Body[0];
    }

    public async Task ReloadConfigAsync(CancellationToken cancellationToken = default)
    {
        await _connection.CallMethodAsync(
            _destination, path, _interface, "ReloadConfig", cancellationToken);
    }

    public async Task<string> GetIdAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetId", cancellationToken);
        return (string)reply.Body[0];
    }

    public async Task<Dictionary<string, DBusVariant>> GetConnectionCredentialsAsync(
        string busName,
        CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, _interface, "GetConnectionCredentials", cancellationToken, busName);
        return (Dictionary<string, DBusVariant>)reply.Body[0];
    }

    // --- Properties ---

    public async Task<List<string>> GetFeaturesPropertyAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, PropertiesInterface, "Get", cancellationToken, _interface, "Features");
        return (List<string>)((DBusVariant)reply.Body[0]).Value;
    }

    public async Task<List<string>> GetInterfacesPropertyAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, PropertiesInterface, "Get", cancellationToken, _interface, "Interfaces");
        return (List<string>)((DBusVariant)reply.Body[0]).Value;
    }

    public async Task<OrgFreedesktopDBusProperties> GetAllPropertiesAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _connection.CallMethodAsync(
            _destination, path, PropertiesInterface, "GetAll", cancellationToken, _interface);
        return ReadProperties((Dictionary<string, DBusVariant>)reply.Body[0]);
    }

    public Task<IDisposable> WatchPropertiesChangedAsync(
        Action<OrgFreedesktopDBusProperties, string[], string[]> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender, path, PropertiesInterface, "PropertiesChanged",
            message =>
            {
                if (!string.Equals((string)message.Body[0], _interface, StringComparison.Ordinal))
                    return Task.CompletedTask;

                var changed = new List<string>();
                var props = ReadProperties((Dictionary<string, DBusVariant>)message.Body[1], changed);
                var invalidated = (List<string>)message.Body[2];
                handler(props, invalidated.ToArray(), changed.ToArray());
                return Task.CompletedTask;
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    // --- Signals ---

    public Task<IDisposable> WatchNameOwnerChangedAsync(
        Action<string, string?, string?> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender, path, _interface, "NameOwnerChanged",
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
            sender, path, _interface, "NameOwnerChanged",
            message =>
            {
                var name = (string)message.Body[0];
                var oldOwner = NormalizeNameOwner((string)message.Body[1]);
                var newOwner = NormalizeNameOwner((string)message.Body[2]);
                return handler(name, oldOwner, newOwner);
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    public Task<IDisposable> WatchNameLostAsync(
        Action<string> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender, path, _interface, "NameLost",
            message =>
            {
                handler((string)message.Body[0]);
                return Task.CompletedTask;
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    public Task<IDisposable> WatchNameAcquiredAsync(
        Action<string> handler,
        string? sender = null,
        bool emitOnCapturedContext = true)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return _connection.SubscribeAsync(
            sender, path, _interface, "NameAcquired",
            message =>
            {
                handler((string)message.Body[0]);
                return Task.CompletedTask;
            },
            emitOnCapturedContext ? SynchronizationContext.Current : null);
    }

    // --- Helpers ---

    public sealed class OrgFreedesktopDBusProperties
    {
        public List<string>? Features { get; set; }
        public List<string>? Interfaces { get; set; }
    }

    private static OrgFreedesktopDBusProperties ReadProperties(
        Dictionary<string, DBusVariant> values,
        List<string>? changed = null)
    {
        var props = new OrgFreedesktopDBusProperties();
        foreach (var entry in values)
        {
            switch (entry.Key)
            {
                case "Features":
                    props.Features = (List<string>)entry.Value.Value;
                    changed?.Add("Features");
                    break;
                case "Interfaces":
                    props.Interfaces = (List<string>)entry.Value.Value;
                    changed?.Add("Interfaces");
                    break;
            }
        }

        return props;
    }

    private static string? NormalizeNameOwner(string owner) =>
        string.IsNullOrWhiteSpace(owner) ? null : owner;

    private static uint CastUInt32(object value) => value switch
    {
        uint u => u,
        int i => unchecked((uint)i),
        _ => throw new InvalidOperationException($"Expected uint32 but got {value?.GetType()?.Name ?? "null"}.")
    };
}