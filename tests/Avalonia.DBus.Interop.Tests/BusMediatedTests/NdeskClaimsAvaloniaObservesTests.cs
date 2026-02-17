using System;
using System.Threading.Tasks;
using Avalonia.DBus.Interop.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.DBus.Interop.Tests.BusMediatedTests;

[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class NdeskClaimsAvaloniaObservesTests(InteropFixture fixture, ITestOutputHelper output)
{
    private static string TestName() => $"org.avalonia.dbus.interop.t{Guid.NewGuid():N}";

    [InteropFact]
    public async Task NdeskRequestsName_AvaloniaSeesViaNameHasOwner()
    {
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
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
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
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
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
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
        await using var conn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var bus = fixture.RequireLoggedNdeskBus(output);
        var name = TestName();

        bus.RequestName(name);
        bus.ReleaseName(name);

        Assert.False(await conn.NameHasOwnerAsync(name));
    }
}
