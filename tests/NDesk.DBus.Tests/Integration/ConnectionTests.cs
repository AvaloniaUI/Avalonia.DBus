using NDesk.DBus.Tests.Helpers;
using org.freedesktop.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Integration;

[Collection(NdeskTestCollection.Name)]
[Trait("Category", "Integration")]
public class ConnectionTests(NdeskBusFixture fixture)
{
    [IntegrationFact]
    public void UniqueName_IsAssigned_StartsWithColon()
    {
        var bus = fixture.RequireBus();

        Assert.StartsWith(":", bus.UniqueName);
    }

    [IntegrationFact]
    public void ListNames_ViaIBusProxy_ContainsFreedesktopDBus()
    {
        var bus = fixture.RequireBus();
        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        var names = dbusBus.ListNames();

        Assert.Contains("org.freedesktop.DBus", names);
    }
}
