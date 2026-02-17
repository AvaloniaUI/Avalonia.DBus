using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Helpers;
using NDesk.DBus;
using org.freedesktop.DBus;
using Xunit;

namespace Avalonia.DBus.Interop.Tests.BusMediatedTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class CrossSignalTests(InteropFixture fixture)
{
    private static string TestName() => $"org.avalonia.dbus.interop.sig.t{Guid.NewGuid():N}";

    [InteropFact]
    public async Task AvaloniaClaimsName_NdeskReceivesNameOwnerChanged()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        string? receivedName = null;
        string? receivedOldOwner = null;
        string? receivedNewOwner = null;
        var signalReceived = new ManualResetEventSlim();

        dbusBus.NameOwnerChanged += (n, oldOwner, newOwner) =>
        {
            if (n != name) return;
            receivedName = n;
            receivedOldOwner = oldOwner;
            receivedNewOwner = newOwner;
            signalReceived?.Set();
        };

        try
        {
            await conn.RequestNameAsync(name);

            // The shared bus has a pump thread running Iterate() which
            // dispatches signals. Wait for it rather than calling Iterate()
            // from the test thread (which would conflict with the pump).
            Assert.True(
                signalReceived.Wait(TimeSpan.FromSeconds(5)),
                "Timed out waiting for NameOwnerChanged signal via NDesk");

            Assert.Equal(name, receivedName);
            Assert.Equal("", receivedOldOwner);
            Assert.NotNull(receivedNewOwner);
            Assert.NotEqual("", receivedNewOwner);
        }
        finally
        {
            signalReceived.Dispose();
            try { await conn.ReleaseNameAsync(name); } catch { }
        }
    }

    [InteropFact]
    public async Task NdeskClaimsName_AvaloniaReceivesNameOwnerChanged()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        var tcs = new TaskCompletionSource<(string Name, string? OldOwner, string? NewOwner)>();

        using var subscription = await conn.WatchNameOwnerChangedAsync(
            (n, oldOwner, newOwner) =>
            {
                if (n == name)
                    tcs.TrySetResult((n, oldOwner, newOwner));
            },
            emitOnCapturedContext: false);

        try
        {
            bus.RequestName(name);

            var result = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.True(result == tcs.Task, "Timed out waiting for NameOwnerChanged signal");

            var (receivedName, receivedOldOwner, receivedNewOwner) = tcs.Task.Result;
            Assert.Equal(name, receivedName);
            Assert.True(string.IsNullOrEmpty(receivedOldOwner));
            Assert.NotNull(receivedNewOwner);
            Assert.NotEqual("", receivedNewOwner);
        }
        finally
        {
            try { bus.ReleaseName(name); } catch { }
        }
    }
}
