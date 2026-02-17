using System;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Interop.Tests.BusMediatedTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class NdeskClaimsAvaloniaObservesTests(InteropFixture fixture)
{
    private static string TestName() => $"org.avalonia.dbus.interop.t{Guid.NewGuid():N}";

    [InteropFact]
    public async Task NdeskRequestsName_AvaloniaSeesViaNameHasOwner()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        try
        {
            bus.RequestName(name);

            Assert.True(await conn.NameHasOwnerAsync(name));
        }
        finally
        {
            try { bus.ReleaseName(name); } catch { }
        }
    }

    [InteropFact]
    public async Task NdeskRequestsName_AvaloniaSeesViaGetNameOwner()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        try
        {
            bus.RequestName(name);

            var owner = await conn.GetNameOwnerAsync(name);
            Assert.NotNull(owner);
            Assert.StartsWith(":", owner);
        }
        finally
        {
            try { bus.ReleaseName(name); } catch { }
        }
    }

    [InteropFact]
    public async Task NdeskRequestsName_AvaloniaSeesViaListNames()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        try
        {
            bus.RequestName(name);

            var names = await conn.ListNamesAsync();
            Assert.Contains(name, names);
        }
        finally
        {
            try { bus.ReleaseName(name); } catch { }
        }
    }

    [InteropFact]
    public async Task NdeskReleasesName_AvaloniaSeesAbsence()
    {
        var conn = fixture.RequireAvaloniaConnection();
        var bus = fixture.RequireNdeskBus();
        var name = TestName();

        bus.RequestName(name);
        bus.ReleaseName(name);

        Assert.False(await conn.NameHasOwnerAsync(name));
    }
}
