using System;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Helpers;
using NDesk.DBus;
using org.freedesktop.DBus;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.DBus.Interop.Tests.BusMediatedTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class AvaloniaClaimsNdeskObservesTests(InteropFixture fixture, ITestOutputHelper output)
{
    private static string TestName() => $"org.avalonia.dbus.interop.t{Guid.NewGuid():N}";

    private IBus GetNdeskDbusBus(Bus bus)
    {
        return bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));
    }

    [InteropFact]
    public async Task AvaloniaRequestsName_NdeskSeesViaNameHasOwner()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
        var name = TestName();

        try
        {
            var reply = await conn.RequestNameAsync(name);
            Assert.Equal(DBusRequestNameReply.PrimaryOwner, reply);

            var dbusBus = GetNdeskDbusBus(bus);
            Assert.True(dbusBus.NameHasOwner(name));
        }
        finally
        {
            try { await conn.ReleaseNameAsync(name); } catch { }
        }
    }

    [InteropFact]
    public async Task AvaloniaRequestsName_NdeskSeesViaGetNameOwner()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
        var name = TestName();

        try
        {
            await conn.RequestNameAsync(name);

            var avaloniaUniqueName = await conn.GetUniqueNameAsync();
            var dbusBus = GetNdeskDbusBus(bus);
            var owner = dbusBus.GetNameOwner(name);

            Assert.Equal(avaloniaUniqueName, owner);
        }
        finally
        {
            try { await conn.ReleaseNameAsync(name); } catch { }
        }
    }

    [InteropFact]
    public async Task AvaloniaRequestsName_NdeskSeesViaListNames()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
        var name = TestName();

        try
        {
            await conn.RequestNameAsync(name);

            var dbusBus = GetNdeskDbusBus(bus);
            var names = dbusBus.ListNames();

            Assert.Contains(name, names);
        }
        finally
        {
            try { await conn.ReleaseNameAsync(name); } catch { }
        }
    }

    [InteropFact]
    public async Task AvaloniaReleasesName_NdeskSeesAbsence()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
        var name = TestName();

        await conn.RequestNameAsync(name);
        await conn.ReleaseNameAsync(name);

        var dbusBus = GetNdeskDbusBus(bus);
        Assert.False(dbusBus.NameHasOwner(name));
    }
}
