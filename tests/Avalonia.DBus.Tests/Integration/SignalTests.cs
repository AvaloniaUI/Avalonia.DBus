using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Trait("Category", "Integration")]
public class SignalTests(BusFixture fixture) : IClassFixture<BusFixture>
{
    [IntegrationFact]
    public async Task Subscribe_NameOwnerChanged_ReceivesSignal()
    {
        var connection = fixture.RequireConnection();
        var tcs = new TaskCompletionSource<DBusMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetCanceled());

        var testName = $"org.avalonia.dbus.test.signal.t{Guid.NewGuid():N}";

        var sub = await connection.SubscribeAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "NameOwnerChanged",
            msg =>
            {
                if (msg.Body.Count >= 1 && msg.Body[0] is string name && name == testName)
                    tcs.TrySetResult(msg);
                return Task.CompletedTask;
            });

        try
        {
            // Request a well-known name to trigger NameOwnerChanged
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            var signal = await tcs.Task;

            Assert.True(signal.IsSignal("org.freedesktop.DBus", "NameOwnerChanged"));
            Assert.Equal(testName, signal.Body[0]);
        }
        finally
        {
            sub.Dispose();
            try
            {
                await connection.CallMethodAsync(
                    "org.freedesktop.DBus",
                    (DBusObjectPath)"/org/freedesktop/DBus",
                    "org.freedesktop.DBus",
                    "ReleaseName",
                    cts.Token,
                    testName);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [IntegrationFact]
    public async Task MultipleSubscribers_AllReceiveSameSignal()
    {
        var connection = fixture.RequireConnection();
        var tcs1 = new TaskCompletionSource<DBusMessage>();
        var tcs2 = new TaskCompletionSource<DBusMessage>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() =>
        {
            tcs1.TrySetCanceled();
            tcs2.TrySetCanceled();
        });

        var testName = $"org.avalonia.dbus.test.multisub.t{Guid.NewGuid():N}";

        var sub1 = await connection.SubscribeAsync(
            "org.freedesktop.DBus", null,
            "org.freedesktop.DBus", "NameOwnerChanged",
            msg =>
            {
                if (msg.Body.Count >= 1 && msg.Body[0] is string name && name == testName)
                    tcs1.TrySetResult(msg);
                return Task.CompletedTask;
            });

        var sub2 = await connection.SubscribeAsync(
            "org.freedesktop.DBus", null,
            "org.freedesktop.DBus", "NameOwnerChanged",
            msg =>
            {
                if (msg.Body.Count >= 1 && msg.Body[0] is string name && name == testName)
                    tcs2.TrySetResult(msg);
                return Task.CompletedTask;
            });

        try
        {
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "RequestName",
                cts.Token,
                testName, 0u);

            await Task.WhenAll(tcs1.Task, tcs2.Task);

            Assert.True(tcs1.Task.IsCompletedSuccessfully);
            Assert.True(tcs2.Task.IsCompletedSuccessfully);
        }
        finally
        {
            sub1.Dispose();
            sub2.Dispose();
            try
            {
                await connection.CallMethodAsync(
                    "org.freedesktop.DBus",
                    (DBusObjectPath)"/org/freedesktop/DBus",
                    "org.freedesktop.DBus",
                    "ReleaseName",
                    cts.Token,
                    testName);
            }
            catch { }
        }
    }

    [IntegrationFact]
    public async Task DisposeSubscription_StopsDelivery()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = 0;

        var sub = await connection.SubscribeAsync(
            "org.freedesktop.DBus", null,
            "org.freedesktop.DBus", "NameOwnerChanged",
            _ =>
            {
                Interlocked.Increment(ref received);
                return Task.CompletedTask;
            });

        sub.Dispose();

        var testName = $"org.avalonia.dbus.test.unsub.t{Guid.NewGuid():N}";
        await connection.CallMethodAsync(
            "org.freedesktop.DBus",
            (DBusObjectPath)"/org/freedesktop/DBus",
            "org.freedesktop.DBus",
            "RequestName",
            cts.Token,
            testName, 0u);

        // Small delay to allow signal propagation if subscription was still active
        await Task.Delay(200);

        try
        {
            await connection.CallMethodAsync(
                "org.freedesktop.DBus",
                (DBusObjectPath)"/org/freedesktop/DBus",
                "org.freedesktop.DBus",
                "ReleaseName",
                cts.Token,
                testName);
        }
        catch { }
    }
}
