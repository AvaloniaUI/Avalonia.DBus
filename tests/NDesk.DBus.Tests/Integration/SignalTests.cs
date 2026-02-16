using System;
using NDesk.DBus.Tests.Helpers;
using org.freedesktop.DBus;
using Xunit;

namespace NDesk.DBus.Tests.Integration;

[Collection(NdeskTestCollection.Name)]
[Trait("Category", "Integration")]
public class SignalTests(NdeskBusFixture fixture)
{
    [IntegrationFact]
    public void Subscribe_NameOwnerChanged_ReceivesSignal()
    {
        var bus = fixture.RequireBus();
        var testName = $"org.ndesk.dbus.test.signal.t{Guid.NewGuid():N}";

        var dbusBus = bus.GetObject<IBus>(
            "org.freedesktop.DBus",
            new ObjectPath("/org/freedesktop/DBus"));

        string receivedName = null;
        string receivedOldOwner = null;
        string receivedNewOwner = null;

        dbusBus.NameOwnerChanged += (name, oldOwner, newOwner) =>
        {
            if (name != testName) return;
            receivedName = name;
            receivedOldOwner = oldOwner;
            receivedNewOwner = newOwner;
        };

        try
        {
            bus.RequestName(testName);

            // Process messages until we get our signal (with timeout)
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (receivedName == null && DateTime.UtcNow < deadline)
                bus.Iterate();

            Assert.Equal(testName, receivedName);
            Assert.NotNull(receivedNewOwner);
            Assert.Equal("", receivedOldOwner);
        }
        finally
        {
            try { bus.ReleaseName(testName); } catch { }
        }
    }
}
