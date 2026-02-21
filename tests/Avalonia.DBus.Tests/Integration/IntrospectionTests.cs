using System.Text;
using System.Threading.Tasks;
using Avalonia.DBus.Tests.Helpers;
using Xunit;

namespace Avalonia.DBus.Tests.Integration;

[Collection(DbusTestCollection.Name)]
[Trait("Category", "Integration")]
public class IntrospectionTests(BusFixture fixture)
{
    private const string TestInterfaceName = "org.avalonia.dbus.tests.Introspection";
    private const string IntrospectableInterfaceName = "org.freedesktop.DBus.Introspectable";

    private static readonly object MetadataGate = new();
    private static bool s_metadataRegistered;

    [IntegrationFact]
    public async Task Introspect_IncludesMethodsAndProperties()
    {
        EnsureMetadataRegistered();

        await using var serverConnection = await fixture.CreateConnectionAsync();
        await using var clientConnection = await fixture.CreateConnectionAsync();
        var serverName = await serverConnection.GetUniqueNameAsync();
        Assert.NotNull(serverName);

        const string path = "/org/avalonia/test/introspection";
        using var _ = await serverConnection.RegisterObjects((DBusObjectPath)path, [new IntrospectionTarget()]);

        var reply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)path,
            IntrospectableInterfaceName,
            "Introspect");

        var xml = Assert.IsType<string>(Assert.Single(reply.Body));

        // Interface should be present
        Assert.Contains($"<interface name=\"{TestInterfaceName}\">", xml, System.StringComparison.Ordinal);

        // Properties
        Assert.Contains("<property name=\"Version\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("type=\"u\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("access=\"read\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("<property name=\"Label\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("type=\"s\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("access=\"readwrite\"", xml, System.StringComparison.Ordinal);

        // Methods
        Assert.Contains("<method name=\"GetInfo\">", xml, System.StringComparison.Ordinal);
        Assert.Contains("<arg type=\"i\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("direction=\"in\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("direction=\"out\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("<method name=\"Ping\"/>", xml, System.StringComparison.Ordinal);

        // Signals
        Assert.Contains("<signal name=\"Changed\">", xml, System.StringComparison.Ordinal);
        Assert.Contains("<arg type=\"b\"", xml, System.StringComparison.Ordinal);
        Assert.Contains("<signal name=\"Closed\"/>", xml, System.StringComparison.Ordinal);

        // Standard interfaces should also be present
        Assert.Contains("<interface name=\"org.freedesktop.DBus.Introspectable\">", xml, System.StringComparison.Ordinal);
        Assert.Contains("<interface name=\"org.freedesktop.DBus.Properties\">", xml, System.StringComparison.Ordinal);
    }

    [IntegrationFact]
    public async Task Introspect_WithoutWriteIntrospectionXml_EmitsEmptyInterface()
    {
        // Register a handler without WriteIntrospectionXml to verify fallback
        const string bareName = "org.avalonia.dbus.tests.BareIntrospection";
        var bareGate = new object();
        lock (bareGate)
        {
            DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
            {
                ClrType = typeof(BareTarget),
                InterfaceName = bareName,
                CreateHandler = static () => new StubDispatcher()
            });
        }

        await using var serverConnection = await fixture.CreateConnectionAsync();
        await using var clientConnection = await fixture.CreateConnectionAsync();
        var serverName = await serverConnection.GetUniqueNameAsync();
        Assert.NotNull(serverName);

        const string path = "/org/avalonia/test/bare";
        using var _ = await serverConnection.RegisterObjects((DBusObjectPath)path, [new BareTarget()]);

        var reply = await clientConnection.CallMethodAsync(
            serverName!,
            (DBusObjectPath)path,
            IntrospectableInterfaceName,
            "Introspect");

        var xml = Assert.IsType<string>(Assert.Single(reply.Body));

        // Interface name should appear but with no children (empty stub)
        Assert.Contains($"<interface name=\"{bareName}\">", xml, System.StringComparison.Ordinal);
        Assert.Contains("</interface>", xml, System.StringComparison.Ordinal);
        Assert.DoesNotContain("<method", xml.Split(bareName)[1].Split("</interface>")[0], System.StringComparison.Ordinal);
    }

    private static void EnsureMetadataRegistered()
    {
        if (s_metadataRegistered)
            return;

        lock (MetadataGate)
        {
            if (s_metadataRegistered)
                return;

            DBusInteropMetadataRegistry.Register(new DBusInteropMetadata
            {
                ClrType = typeof(IntrospectionTarget),
                InterfaceName = TestInterfaceName,
                CreateHandler = static () => new StubDispatcher(),
                WriteIntrospectionXml = static (sb, indent) =>
                {
                    var inner = indent + "  ";
                    sb.Append(indent).Append("<interface name=\"").Append(TestInterfaceName).AppendLine("\">");
                    sb.Append(inner).AppendLine("<property name=\"Version\" type=\"u\" access=\"read\"/>");
                    sb.Append(inner).AppendLine("<property name=\"Label\" type=\"s\" access=\"readwrite\"/>");
                    sb.Append(inner).AppendLine("<method name=\"GetInfo\">");
                    sb.Append(inner).AppendLine("  <arg type=\"i\" name=\"id\" direction=\"in\"/>");
                    sb.Append(inner).AppendLine("  <arg type=\"s\" direction=\"out\"/>");
                    sb.Append(inner).AppendLine("</method>");
                    sb.Append(inner).AppendLine("<method name=\"Ping\"/>");
                    sb.Append(inner).AppendLine("<signal name=\"Changed\">");
                    sb.Append(inner).AppendLine("  <arg type=\"b\" name=\"new_value\"/>");
                    sb.Append(inner).AppendLine("</signal>");
                    sb.Append(inner).AppendLine("<signal name=\"Closed\"/>");
                    sb.Append(indent).AppendLine("</interface>");
                }
            });

            s_metadataRegistered = true;
        }
    }

    private sealed class IntrospectionTarget;
    private sealed class BareTarget;

    private sealed class StubDispatcher : IDBusInterfaceCallDispatcher
    {
        public Task<DBusMessage> Handle(IDBusConnection _, object? __, DBusMessage message)
        {
            return Task.FromResult(message.CreateError(
                "org.freedesktop.DBus.Error.UnknownMethod",
                "Unknown method"));
        }
    }
}
