using System;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class ObjectRegistrationTests(BusFixture fixture)
{
    private const string TestInterfaceName = "org.avalonia.dbus.tests.VirtualPath";
    private const string IntrospectableInterfaceName = "org.freedesktop.DBus.Introspectable";

    private static readonly object MetadataGate = new();
    private static bool s_metadataRegistered;

    [IntegrationFact]
    public async Task CallUnknownObject_ReturnsError()
    {
        var connection = fixture.RequireConnection();
        var myName = await connection.GetUniqueNameAsync();
        Assert.NotNull(myName);

        var ex = await Assert.ThrowsAsync<DBusException>(async () =>
            await connection.CallMethodAsync(
                myName!,
                (DBusObjectPath)"/nonexistent/object/path",
                "org.test.Iface",
                "Method"));

        Assert.Contains("UnknownObject", ex.ErrorName);
    }

    [IntegrationFact]
    public async Task Introspect_VirtualParentPaths_ReturnsChildrenAndLeafInterface()
    {
        EnsureVirtualPathMetadataRegistered();

        await using var serverConnection = await fixture.CreateConnectionAsync();
        await using var clientConnection = await fixture.CreateConnectionAsync();
        var serverName = await serverConnection.GetUniqueNameAsync();
        Assert.NotNull(serverName);

        const string leafPath = "/org/a11y/atspi/accessible/root";
        using var _ = await serverConnection.RegisterObjects((DBusObjectPath)leafPath, [new VirtualPathTarget()]);

        var rootReply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)"/",
            IntrospectableInterfaceName,
            "Introspect");
        var rootXml = Assert.IsType<string>(Assert.Single(rootReply.Body));
        Assert.Contains("<node name=\"org\"/>", rootXml, StringComparison.Ordinal);

        var orgReply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)"/org",
            IntrospectableInterfaceName,
            "Introspect");
        var orgXml = Assert.IsType<string>(Assert.Single(orgReply.Body));
        Assert.Contains("<node name=\"a11y\"/>", orgXml, StringComparison.Ordinal);

        var a11yReply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)"/org/a11y",
            IntrospectableInterfaceName,
            "Introspect");
        var a11yXml = Assert.IsType<string>(Assert.Single(a11yReply.Body));
        Assert.Contains("<node name=\"atspi\"/>", a11yXml, StringComparison.Ordinal);

        var leafReply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)leafPath,
            IntrospectableInterfaceName,
            "Introspect");
        var leafXml = Assert.IsType<string>(Assert.Single(leafReply.Body));
        Assert.Contains($"<interface name=\"{TestInterfaceName}\">", leafXml, StringComparison.Ordinal);
    }

    private static void EnsureVirtualPathMetadataRegistered()
    {
        if (s_metadataRegistered)
            return;

        lock (MetadataGate)
        {
            if (s_metadataRegistered)
                return;

            DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
            {
                ClrType = typeof(VirtualPathTarget),
                InterfaceName = TestInterfaceName,
                CreateHandler = static () => new VirtualPathDispatcher()
            });

            s_metadataRegistered = true;
        }
    }

    private sealed class VirtualPathTarget;

    private sealed class VirtualPathDispatcher : IDBusInterfaceCallDispatcher
    {
        public Task<DBusMessage> Handle(IDBusConnection _, object? __, DBusMessage message)
        {
            return Task.FromResult(message.CreateError(
                "org.freedesktop.DBus.Error.UnknownMethod",
                "Unknown method"));
        }
    }
}
