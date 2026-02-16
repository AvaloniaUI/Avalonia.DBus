using System;
using NDesk.DBus.Tests.Helpers;
using org.freedesktop.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Integration;

[Collection(NdeskTestCollection.Name)]
[Trait("Category", "Integration")]
public class NameOwnershipTests(NdeskBusFixture fixture)
{
    [IntegrationFact]
    public void RequestName_UniqueName_ReturnsPrimaryOwner()
    {
        var bus = fixture.RequireBus();
        var testName = $"org.ndesk.dbus.test.t{Guid.NewGuid():N}";

        try
        {
            var reply = bus.RequestName(testName);

            Assert.Equal(RequestNameReply.PrimaryOwner, reply);
        }
        finally
        {
            try
            {
                bus.ReleaseName(testName);

            }
            catch
            {
                // ignored
            }
        }
    }

    [IntegrationFact]
    public void RequestName_ThenGetNameOwner_ReturnsOurUniqueName()
    {
        var bus = fixture.RequireBus();
        var testName = $"org.ndesk.dbus.test.t{Guid.NewGuid():N}";
        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        try
        {
            bus.RequestName(testName);

            var owner = dbusBus.GetNameOwner(testName);

            Assert.Equal(bus.UniqueName, owner);
        }
        finally
        {
            try { bus.ReleaseName(testName); } catch { }
        }
    }

    [IntegrationFact]
    public void ReleaseName_AfterRequest_ReturnsReleased()
    {
        var bus = fixture.RequireBus();
        var testName = $"org.ndesk.dbus.test.t{Guid.NewGuid():N}";

        bus.RequestName(testName);

        var reply = bus.ReleaseName(testName);

        Assert.Equal(ReleaseNameReply.Released, reply);
    }
}
