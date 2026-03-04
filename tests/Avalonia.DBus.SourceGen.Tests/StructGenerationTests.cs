using System.Xml.Linq;

namespace Avalonia.DBus.SourceGen.Tests;

public class StructGenerationTests
{
    [Fact]
    public void AvTypesDocument_Deserializes_Structs()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <Struct Name="TestPoint">
                <Property Name="X"/>
                <Property Name="Y"/>
              </Struct>
            </Types>
            """;

        var doc = XDocumentParser.ParseTypesDocument(XDocument.Parse(xml));

        Assert.NotNull(doc);
        var s = Assert.Single(doc!.Structs!);
        Assert.Equal("TestPoint", s.Name);
        Assert.Equal(new[] { "X", "Y" }, s.Properties!.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void AvTypesDocument_Deserializes_Dictionaries()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <Dictionary Name="TestDict">
                <Key Name="Id"/>
                <Value Name="Data"/>
              </Dictionary>
            </Types>
            """;

        var doc = XDocumentParser.ParseTypesDocument(XDocument.Parse(xml));

        Assert.NotNull(doc);
        var d = Assert.Single(doc!.Dictionaries!);
        Assert.Equal("TestDict", d.Name);
        Assert.Equal("Id", d.Key!.Name);
        Assert.Equal("Data", d.Value!.Name);
    }

    [Fact]
    public void AvTypesDocument_Deserializes_BitFlags()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="Read" Value="1"/>
                <BitFlag Name="Write" Value="2"/>
                <BitFlag Name="Execute" Value="4"/>
              </BitFlags>
            </Types>
            """;

        var doc = XDocumentParser.ParseTypesDocument(XDocument.Parse(xml));

        Assert.NotNull(doc);
        var bf = Assert.Single(doc!.BitFlags!);
        Assert.Equal("TestFlags", bf.Name);
        Assert.Equal(3, bf.BitFlags!.Length);
    }

    [Fact]
    public void AvTypesDocument_Deserializes_NestedStructs()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <Struct Name="Outer">
                <Property Name="Inner" Type="InnerStruct"/>
                <Property Name="Value"/>
              </Struct>
              <Struct Name="InnerStruct">
                <Property Name="X"/>
                <Property Name="Y"/>
              </Struct>
            </Types>
            """;

        var doc = XDocumentParser.ParseTypesDocument(XDocument.Parse(xml));

        Assert.NotNull(doc);
        Assert.Equal(2, doc!.Structs!.Length);
        Assert.Equal("InnerStruct", doc.Structs[0].Properties![0].Type);
    }

    [Fact]
    public void DBusNode_Deserializes_Correctly()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Simple">
                <method name="Ping"/>
                <property name="Status" type="s" access="read"/>
                <signal name="StatusChanged">
                  <arg name="newStatus" type="s"/>
                </signal>
              </interface>
            </node>
            """;

        var node = XDocumentParser.ParseNode(XDocument.Parse(xml));

        Assert.NotNull(node);
        Assert.Single(node!.Interfaces!);

        var iface = node.Interfaces![0];
        Assert.Equal("org.test.Simple", iface.Name);
        var method = Assert.Single(iface.Methods!);
        Assert.Equal("Ping", method.Name);
        var prop = Assert.Single(iface.Properties!);
        Assert.Equal("Status", prop.Name);
        Assert.Equal("s", prop.Type);
        Assert.Equal("read", prop.Access);
        var signal = Assert.Single(iface.Signals!);
        Assert.Equal("StatusChanged", signal.Name);
    }

    [Fact]
    public void DBusInterface_Methods_WithArgs()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Args">
                <method name="Transfer">
                  <arg direction="in" name="source" type="s"/>
                  <arg direction="in" name="amount" type="i"/>
                  <arg direction="out" name="success" type="b"/>
                </method>
              </interface>
            </node>
            """;

        var node = XDocumentParser.ParseNode(XDocument.Parse(xml));

        Assert.NotNull(node);
        Assert.NotNull(node!.Interfaces);
        var method = node.Interfaces[0].Methods?[0];
        Assert.NotNull(method);
        Assert.Equal("Transfer", method.Name);
        Assert.Equal(3, method.Arguments!.Length);
        Assert.Equal("in", method.Arguments[0].Direction);
        Assert.Equal("source", method.Arguments[0].Name);
        Assert.Equal("s", method.Arguments[0].Type);
        Assert.Equal("out", method.Arguments[2].Direction);
    }
}
