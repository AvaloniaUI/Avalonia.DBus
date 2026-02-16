using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class SignalTests(BusFixture fixture)
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

}
