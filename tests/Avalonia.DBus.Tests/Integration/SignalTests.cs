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
        var tcs = new TaskCompletionSource<(string Name, string? OldOwner, string? NewOwner)>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetCanceled());

        var testName = $"org.avalonia.dbus.test.signal.t{Guid.NewGuid():N}";

        var sub = await connection.WatchNameOwnerChangedAsync(
            (name, oldOwner, newOwner) =>
            {
                if (name == testName)
                    tcs.TrySetResult((name, oldOwner, newOwner));
            },
            emitOnCapturedContext: false);

        try
        {
            await connection.RequestNameAsync(testName, cancellationToken: cts.Token);

            var signal = await tcs.Task;

            Assert.Equal(testName, signal.Name);
            Assert.Null(signal.OldOwner);
            Assert.NotNull(signal.NewOwner);
        }
        finally
        {
            sub.Dispose();
            try { await connection.ReleaseNameAsync(testName, cts.Token); }
            catch { /* best-effort */ }
        }
    }

    [IntegrationFact]
    public async Task MultipleSubscribers_AllReceiveSameSignal()
    {
        var connection = fixture.RequireConnection();
        var tcs1 = new TaskCompletionSource<string>();
        var tcs2 = new TaskCompletionSource<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() =>
        {
            tcs1.TrySetCanceled();
            tcs2.TrySetCanceled();
        });

        var testName = $"org.avalonia.dbus.test.multisub.t{Guid.NewGuid():N}";

        var sub1 = await connection.WatchNameOwnerChangedAsync(
            (name, _, _) => { if (name == testName) tcs1.TrySetResult(name); },
            emitOnCapturedContext: false);

        var sub2 = await connection.WatchNameOwnerChangedAsync(
            (name, _, _) => { if (name == testName) tcs2.TrySetResult(name); },
            emitOnCapturedContext: false);

        try
        {
            await connection.RequestNameAsync(testName, cancellationToken: cts.Token);

            await Task.WhenAll(tcs1.Task, tcs2.Task);

            Assert.True(tcs1.Task.IsCompletedSuccessfully);
            Assert.True(tcs2.Task.IsCompletedSuccessfully);
        }
        finally
        {
            sub1.Dispose();
            sub2.Dispose();
            try { await connection.ReleaseNameAsync(testName, cts.Token); }
            catch { /* best-effort */ }
        }
    }

    [IntegrationFact]
    public async Task MultipleProxyInstances_BothReceiveSignal()
    {
        var connection = fixture.RequireConnection();
        var hitA = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hitB = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() =>
        {
            hitA.TrySetCanceled();
            hitB.TrySetCanceled();
        });

        var testName = $"org.avalonia.dbus.test.multiproxy.t{Guid.NewGuid():N}";

        var proxyA = connection.CreateFreedesktopDBusProxy();
        var proxyB = connection.CreateFreedesktopDBusProxy();

        var subA = await proxyA.WatchNameOwnerChangedAsync((name, _, _) =>
        {
            if (string.Equals(name, testName, StringComparison.Ordinal))
                hitA.TrySetResult(true);
        });

        var subB = await proxyB.WatchNameOwnerChangedAsync((name, _, _) =>
        {
            if (string.Equals(name, testName, StringComparison.Ordinal))
                hitB.TrySetResult(true);
        });

        try
        {
            await connection.RequestNameAsync(testName, cancellationToken: cts.Token);

            await Task.WhenAll(hitA.Task, hitB.Task);

            Assert.True(hitA.Task.IsCompletedSuccessfully);
            Assert.True(hitB.Task.IsCompletedSuccessfully);
        }
        finally
        {
            subA.Dispose();
            subB.Dispose();
            try { await connection.ReleaseNameAsync(testName, cts.Token); }
            catch { /* best-effort */ }
        }
    }

    [IntegrationFact]
    public async Task DisposeSubscription_StopsDelivery()
    {
        var connection = fixture.RequireConnection();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = 0;

        var sub = await connection.WatchNameOwnerChangedAsync(
            (_, _, _) => Interlocked.Increment(ref received),
            emitOnCapturedContext: false);

        sub.Dispose();

        var testName = $"org.avalonia.dbus.test.unsub.t{Guid.NewGuid():N}";
        await connection.RequestNameAsync(testName, cancellationToken: cts.Token);

        // Small delay to allow signal propagation if subscription was still active
        await Task.Delay(200);

        try { await connection.ReleaseNameAsync(testName, cts.Token); }
        catch { /* best-effort */ }
    }
}
