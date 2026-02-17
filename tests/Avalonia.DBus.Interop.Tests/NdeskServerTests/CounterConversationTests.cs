using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Contracts;
using Avalonia.DBus.Interop.Tests.Helpers;
using NDesk.DBus;
using Xunit;

namespace Avalonia.DBus.Interop.Tests.NdeskServerTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class CounterConversationTests(InteropFixture fixture)
{
    private const string CounterInterface = "org.avalonia.dbus.interop.Counter";
    private static readonly DBusObjectPath CounterPath = (DBusObjectPath)"/org/avalonia/dbus/interop/Counter";
    private static readonly ObjectPath NdeskCounterPath = new("/org/avalonia/dbus/interop/Counter");

    private static string TestName() => $"org.avalonia.dbus.interop.counter.t{Guid.NewGuid():N}";

    private async Task<T> CallCounterMethodAsync<T>(
        DBusConnection conn,
        string destination,
        string member,
        CancellationToken ct,
        params object[] args)
    {
        var reply = await conn.CallMethodAsync(
            destination, CounterPath, CounterInterface, member, ct, args);

        Assert.NotEmpty(reply.Body);
        return (T)reply.Body[0];
    }

    private async Task CallCounterVoidAsync(
        DBusConnection conn,
        string destination,
        string member,
        CancellationToken ct)
    {
        await conn.CallMethodAsync(
            destination, CounterPath, CounterInterface, member, ct);
    }

    [InteropFact]
    public async Task IncrementThreeTimes_GetValueReturns3()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var serverBus = fixture.CreateNdeskBus();
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskCounterPath, new CounterService());
        using var runner = new NdeskServerRunner(serverBus);

        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);

        var value = await CallCounterMethodAsync<int>(conn, name, "GetValue", CancellationToken.None);
        Assert.Equal(3, value);
    }

    [InteropFact]
    public async Task IncrementAndDecrement_TracksCorrectValue()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var serverBus = fixture.CreateNdeskBus();
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskCounterPath, new CounterService());
        using var runner = new NdeskServerRunner(serverBus);

        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Decrement", CancellationToken.None);

        var value = await CallCounterMethodAsync<int>(conn, name, "GetValue", CancellationToken.None);
        Assert.Equal(2, value);
    }

    [InteropFact]
    public async Task IncrementTriggersValueChangedSignals()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var serverBus = fixture.CreateNdeskBus();
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskCounterPath, new CounterService());
        using var runner = new NdeskServerRunner(serverBus);

        var signals = new List<(int Old, int New)>();
        var signalCount = new SemaphoreSlim(0);

        using var sub = await conn.SubscribeAsync(
            null, CounterPath, CounterInterface, "ValueChanged",
            msg =>
            {
                var oldVal = (int)msg.Body[0];
                var newVal = (int)msg.Body[1];
                signals.Add((oldVal, newVal));
                signalCount.Release();
                return Task.CompletedTask;
            });

        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);

        // Wait for all 3 signals
        for (var i = 0; i < 3; i++)
            Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), $"Timed out waiting for signal {i + 1}");

        Assert.Equal([(0, 1), (1, 2), (2, 3)], signals);
    }

    [InteropFact]
    public async Task ResetAfterIncrements_SignalShowsDropToZero()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var serverBus = fixture.CreateNdeskBus();
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskCounterPath, new CounterService());
        using var runner = new NdeskServerRunner(serverBus);

        var signals = new List<(int Old, int New)>();
        var signalCount = new SemaphoreSlim(0);

        using var sub = await conn.SubscribeAsync(
            null, CounterPath, CounterInterface, "ValueChanged",
            msg =>
            {
                var oldVal = (int)msg.Body[0];
                var newVal = (int)msg.Body[1];
                signals.Add((oldVal, newVal));
                signalCount.Release();
                return Task.CompletedTask;
            });

        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);

        // Wait for 2 increment signals
        for (var i = 0; i < 2; i++)
            Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), $"Timed out waiting for increment signal {i + 1}");

        await CallCounterVoidAsync(conn, name, "Reset", CancellationToken.None);

        // Wait for the reset signal
        Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), "Timed out waiting for reset signal");

        Assert.Equal(3, signals.Count);
        Assert.Equal((2, 0), signals[2]);
    }

    [InteropFact]
    public async Task FullConversation_MethodsAndSignalsInterleaved()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var serverBus = fixture.CreateNdeskBus();
        var name = TestName();

        serverBus.RequestName(name);
        serverBus.Register(NdeskCounterPath, new CounterService());
        using var runner = new NdeskServerRunner(serverBus);

        var signals = new List<(int Old, int New)>();
        var signalCount = new SemaphoreSlim(0);

        using var sub = await conn.SubscribeAsync(
            null, CounterPath, CounterInterface, "ValueChanged",
            msg =>
            {
                var oldVal = (int)msg.Body[0];
                var newVal = (int)msg.Body[1];
                signals.Add((oldVal, newVal));
                signalCount.Release();
                return Task.CompletedTask;
            });

        // Increment -> expect (0,1)
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), "Timed out waiting for signal after first Increment");
        Assert.Equal((0, 1), signals[0]);

        // Increment -> expect (1,2)
        await CallCounterVoidAsync(conn, name, "Increment", CancellationToken.None);
        Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), "Timed out waiting for signal after second Increment");
        Assert.Equal((1, 2), signals[1]);

        // Decrement -> expect (2,1)
        await CallCounterVoidAsync(conn, name, "Decrement", CancellationToken.None);
        Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), "Timed out waiting for signal after Decrement");
        Assert.Equal((2, 1), signals[2]);

        // Reset -> expect (1,0)
        await CallCounterVoidAsync(conn, name, "Reset", CancellationToken.None);
        Assert.True(await signalCount.WaitAsync(TimeSpan.FromSeconds(5)), "Timed out waiting for signal after Reset");
        Assert.Equal((1, 0), signals[3]);

        // Verify final state
        var value = await CallCounterMethodAsync<int>(conn, name, "GetValue", CancellationToken.None);
        Assert.Equal(0, value);
    }
}
