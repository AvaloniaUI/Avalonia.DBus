using System;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Interop;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Interop")]
public class ResourceCleanupTests(BusFixture fixture)
{
    [IntegrationFact]
    public async Task SignalSubscriptions_CleanedUpOnDispose()
    {
        await using var connection = await fixture.CreateConnectionAsync();

        // Create multiple subscriptions
        var subs = new IDisposable[5];
        for (var i = 0; i < subs.Length; i++)
        {
            subs[i] = await connection.SubscribeAsync(
                "org.freedesktop.DBus",
                null,
                "org.freedesktop.DBus",
                "NameOwnerChanged",
                _ => Task.CompletedTask);
        }

        // Dispose each subscription individually
        foreach (var sub in subs)
            sub.Dispose();

        // Connection should still be usable after disposing subscriptions
        var id = await connection.GetIdAsync();
        Assert.NotEmpty(id);
    }

    [IntegrationFact]
    public async Task ConnectionDispose_CleansUpSubscriptions()
    {
        var connection = await fixture.CreateConnectionAsync();

        // Add subscriptions without individually disposing them
        for (var i = 0; i < 5; i++)
        {
            await connection.SubscribeAsync(
                "org.freedesktop.DBus",
                null,
                "org.freedesktop.DBus",
                "NameOwnerChanged",
                _ => Task.CompletedTask);
        }

        // Disposing connection should clean up all subscriptions
        var disposeTask = connection.DisposeAsync().AsTask();
        var completed = await Task.WhenAny(disposeTask, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed != disposeTask)
            throw new TimeoutException("Connection disposal timed out.");
        await disposeTask;
    }
}
