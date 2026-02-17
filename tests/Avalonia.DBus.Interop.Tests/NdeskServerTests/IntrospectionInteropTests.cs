using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Avalonia.DBus.Interop.Tests.Helpers;
using NDesk.DBus;
using Xunit;
using Xunit.Abstractions;

namespace Avalonia.DBus.Interop.Tests.NdeskServerTests;

/// <summary>
/// Verifies that NDesk.DBus can introspect objects hosted by Avalonia.DBus
/// and that the returned XML contains full interface definitions (methods,
/// properties, signals) — not empty stubs.
/// </summary>
[Collection(InteropTestCollection.Name)]
[Trait("Category", "Interop")]
public class IntrospectionInteropTests(InteropFixture fixture, ITestOutputHelper output)
{
    private const string TestInterfaceName = "org.avalonia.dbus.interop.IntrospectionTest";
    private static readonly DBusObjectPath AvaloniaPath = (DBusObjectPath)"/org/avalonia/dbus/interop/IntrospectionTest";
    private static readonly ObjectPath NdeskPath = new("/org/avalonia/dbus/interop/IntrospectionTest");

    private static readonly object MetadataGate = new();
    private static bool s_metadataRegistered;

    private static string TestName() => $"org.avalonia.dbus.interop.introspect.t{Guid.NewGuid():N}";

    [InteropFact]
    public async Task NdeskIntrospect_AvaloniaServer_ReturnsFullInterface()
    {
        EnsureMetadataRegistered();

        await using var avaloniaConn = await fixture.CreateLoggedAvaloniaConnectionAsync(output);
        var avaloniaName = await avaloniaConn.GetUniqueNameAsync();
        Assert.NotNull(avaloniaName);

        using var _ = avaloniaConn.RegisterObjects(AvaloniaPath, [new IntrospectionTarget()]);

        // Use a separate NDesk bus to call Introspect on the Avalonia-hosted object
        var ndeskBus = fixture.CreateLoggedNdeskBus(output);
        var introspectable = ndeskBus.GetObject<org.freedesktop.DBus.Introspectable>(
            avaloniaName!, NdeskPath);

        var xml = introspectable.Introspect();
        output.WriteLine("Introspection XML:");
        output.WriteLine(xml);

        Assert.NotNull(xml);
        Assert.NotEmpty(xml);

        // Parse as XML to validate well-formedness
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        // Find our test interface
        var ifaceNode = doc.SelectSingleNode(
            $"//interface[@name='{TestInterfaceName}']");
        Assert.NotNull(ifaceNode);

        // Verify properties
        var versionProp = ifaceNode!.SelectSingleNode("property[@name='Version']");
        Assert.NotNull(versionProp);
        Assert.Equal("u", versionProp!.Attributes!["type"]!.Value);
        Assert.Equal("read", versionProp.Attributes["access"]!.Value);

        var labelProp = ifaceNode.SelectSingleNode("property[@name='Label']");
        Assert.NotNull(labelProp);
        Assert.Equal("s", labelProp!.Attributes!["type"]!.Value);
        Assert.Equal("readwrite", labelProp.Attributes["access"]!.Value);

        // Verify methods
        var getInfoMethod = ifaceNode.SelectSingleNode("method[@name='GetInfo']");
        Assert.NotNull(getInfoMethod);
        var getInfoArgs = getInfoMethod!.SelectNodes("arg");
        Assert.NotNull(getInfoArgs);
        Assert.Equal(2, getInfoArgs!.Count);

        var inArg = getInfoMethod.SelectSingleNode("arg[@direction='in']");
        Assert.NotNull(inArg);
        Assert.Equal("i", inArg!.Attributes!["type"]!.Value);
        Assert.Equal("id", inArg.Attributes["name"]!.Value);

        var outArg = getInfoMethod.SelectSingleNode("arg[@direction='out']");
        Assert.NotNull(outArg);
        Assert.Equal("s", outArg!.Attributes!["type"]!.Value);

        var pingMethod = ifaceNode.SelectSingleNode("method[@name='Ping']");
        Assert.NotNull(pingMethod);

        // Verify signals
        var changedSignal = ifaceNode.SelectSingleNode("signal[@name='Changed']");
        Assert.NotNull(changedSignal);
        var signalArg = changedSignal!.SelectSingleNode("arg");
        Assert.NotNull(signalArg);
        Assert.Equal("b", signalArg!.Attributes!["type"]!.Value);

        var closedSignal = ifaceNode.SelectSingleNode("signal[@name='Closed']");
        Assert.NotNull(closedSignal);

        // Standard interfaces must also be present
        Assert.NotNull(doc.SelectSingleNode(
            "//interface[@name='org.freedesktop.DBus.Introspectable']"));
        Assert.NotNull(doc.SelectSingleNode(
            "//interface[@name='org.freedesktop.DBus.Properties']"));
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
