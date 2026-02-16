using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.DBus.Tests.Helpers;

/// <summary>
/// Helper that provides typed echo-like operations using the bus daemon's own
/// methods. Since registering custom objects requires source-generated handlers,
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
        var connection = await DBusConnection.ConnectSessionAsync();
        var busName = $"org.avalonia.dbus.test.srv.t{Guid.NewGuid():N}";

        var nameReply = await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "RequestName",
            ct,
            busName, 0u);

        var code = (uint)nameReply.Body[0];
        if (code != 1u)
            throw new InvalidOperationException($"Failed to acquire bus name '{busName}', reply code: {code}");

        return new TestServer(connection, busName);
    }

    /// <summary>
    /// Round-trips a string through GetNameOwner (send string, receive string).
    /// </summary>
    public async Task<string> EchoStringAsync(string name, CancellationToken ct = default)
    {
        var reply = await _connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "GetNameOwner",
            ct,
            name);

        return (string)reply.Body[0];
    }

    /// <summary>
    /// Round-trips a bool through NameHasOwner (send string, receive bool).
    /// </summary>
    public async Task<bool> CheckNameExistsAsync(string name, CancellationToken ct = default)
    {
        var reply = await _connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameHasOwner",
            ct,
            name);

        return (bool)reply.Body[0];
    }

    /// <summary>
    /// Round-trips a uint32 through RequestName (send string + uint32, receive uint32).
    /// The name is immediately released afterwards.
    /// </summary>
    public async Task<uint> EchoUInt32ViaRequestNameAsync(CancellationToken ct = default)
    {
        var tempName = $"org.avalonia.dbus.test.echo.t{Guid.NewGuid():N}";

        try
        {
            var reply = await _connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                ct,
                tempName, 0u);

            return (uint)reply.Body[0];
        }
        finally
        {
            try
            {
                await _connection.CallMethodAsync(
                    "org.freedesktop.DBus",
                    (DBusObjectPath)"/org/freedesktop/DBus",
                    "org.freedesktop.DBus",
                    "ReleaseName",
                    ct,
                    tempName);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    /// <summary>
    /// Round-trips a string array through ListNames (no args, receive string[]).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken ct = default)
    {
        var reply = await _connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "ListNames",
            ct);

        if (reply.Body[0] is List<string> list)
            return list;
        if (reply.Body[0] is string[] array)
            return array;

        throw new InvalidOperationException($"Unexpected ListNames return type: {reply.Body[0]?.GetType()}");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "ReleaseName",
                default,
                _ownedName);
        }
        catch
        {
            // Best-effort cleanup
        }

        await _connection.DisposeAsync();
    }
}
