using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// Helper that provides typed echo-like operations using the bus daemon's own
/// methods via the built-in OrgFreedesktopDBusProxy and extension methods.
/// Since registering custom objects requires source-generated handlers,
/// this helper uses well-known bus methods (GetNameOwner, RequestName, ListNames,
/// NameHasOwner, etc.) to exercise type round-trips through the wire.
///
/// For tests that need a real registered object, use the source generator
/// in a dedicated test assembly or the sample projects.
/// </summary>
public sealed class TestServer : IAsyncDisposable
{
    private readonly DBusConnection _connection;
    private readonly string _ownedName;

    private TestServer(DBusConnection connection, string ownedName)
    {
        _connection = connection;
        _ownedName = ownedName;
    }

    /// <summary>The well-known bus name this server owns (can be used for GetNameOwner round-trips).</summary>
    public string OwnedName => _ownedName;

    /// <summary>The connection used by this server.</summary>
    public DBusConnection Connection => _connection;

    /// <summary>
    /// Starts a test server on a new session bus connection and claims a unique well-known name.
    /// </summary>
    public static async Task<TestServer> StartAsync(CancellationToken ct = default)
    {
        var connection = await DBusConnection.ConnectSessionAsync(ct);
        var busName = $"org.avalonia.dbus.test.srv.t{Guid.NewGuid():N}";

        var reply = await connection.RequestNameAsync(busName, cancellationToken: ct);
        return reply != DBusRequestNameReply.PrimaryOwner ?
            throw new InvalidOperationException($"Failed to acquire bus name '{busName}', reply: {reply}") : new TestServer(connection, busName);
    }

    /// <summary>
    /// Round-trips a string through GetNameOwner (send string, receive string).
    /// </summary>
    public Task<string?> EchoStringAsync(string name, CancellationToken ct = default)
    {
        return _connection.GetNameOwnerAsync(name, ct);
    }

    /// <summary>
    /// Round-trips a bool through NameHasOwner (send string, receive bool).
    /// </summary>
    public Task<bool> CheckNameExistsAsync(string name, CancellationToken ct = default)
    {
        return _connection.NameHasOwnerAsync(name, ct);
    }

    /// <summary>
    /// Round-trips a uint32 through RequestName (send string + uint32, receive enum).
    /// The name is immediately released afterwards.
    /// </summary>
    public async Task<DBusRequestNameReply> EchoUInt32ViaRequestNameAsync(CancellationToken ct = default)
    {
        var tempName = $"org.avalonia.dbus.test.echo.t{Guid.NewGuid():N}";

        try
        {
            return await _connection.RequestNameAsync(tempName, cancellationToken: ct);
        }
        finally
        {
            try
            {
                await _connection.ReleaseNameAsync(tempName, ct);
            }
            catch
            {
                 /* best-effort */
            }
        }
    }

    /// <summary>
    /// Round-trips a string array through ListNames (no args, receive string[]).
    /// </summary>
    public Task<List<string>> ListNamesAsync(CancellationToken ct = default)
    {
        return _connection.ListNamesAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _connection.ReleaseNameAsync(_ownedName);
        }
        catch
        {
             /* best-effort */
        }

        await _connection.DisposeAsync();
    }
}
