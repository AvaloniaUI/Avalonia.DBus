using NDesk.DBus.Tests.Helpers;
using org.freedesktop.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Integration;

[Collection(NdeskTestCollection.Name)]
[Trait("Category", "Integration")]
public class MethodCallTests(NdeskBusFixture fixture)
{
    [IntegrationFact]
    public void IBus_NameHasOwner_FreedesktopDBus_ReturnsTrue()
    {
        var bus = fixture.RequireBus();
        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        var hasOwner = dbusBus.NameHasOwner("org.freedesktop.DBus");

        Assert.True(hasOwner);
    }

    [IntegrationFact]
    public void IBus_GetNameOwner_FreedesktopDBus_ReturnsSelf()
    {
        var bus = fixture.RequireBus();
        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        var owner = dbusBus.GetNameOwner("org.freedesktop.DBus");

        Assert.Equal("org.freedesktop.DBus", owner);
    }
}
