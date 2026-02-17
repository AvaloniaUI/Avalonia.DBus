using System.Xml.Serialization;

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

        var serializer = new XmlSerializer(typeof(AvTypesDocument));
        using var reader = new StringReader(xml);
        var doc = serializer.Deserialize(reader) as AvTypesDocument;

        Assert.NotNull(doc);
        Assert.NotNull(doc!.Structs);
        Assert.Single(doc.Structs);
        Assert.Equal("TestPoint", doc.Structs[0].Name);
        Assert.Equal(2, doc.Structs[0].Properties!.Length);
        Assert.Equal("X", doc.Structs[0].Properties![0].Name);
        Assert.Equal("Y", doc.Structs[0].Properties![1].Name);
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

        var serializer = new XmlSerializer(typeof(AvTypesDocument));
        using var reader = new StringReader(xml);
        var doc = serializer.Deserialize(reader) as AvTypesDocument;

        Assert.NotNull(doc);
        Assert.NotNull(doc!.Dictionaries);
        Assert.Single(doc.Dictionaries);
        Assert.Equal("TestDict", doc.Dictionaries[0].Name);
        Assert.Equal("Id", doc.Dictionaries[0].Key!.Name);
        Assert.Equal("Data", doc.Dictionaries[0].Value!.Name);
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

        var serializer = new XmlSerializer(typeof(AvTypesDocument));
        using var reader = new StringReader(xml);
        var doc = serializer.Deserialize(reader) as AvTypesDocument;

        Assert.NotNull(doc);
        Assert.NotNull(doc!.BitFlags);
        Assert.Single(doc.BitFlags);
        Assert.Equal("TestFlags", doc.BitFlags[0].Name);
        Assert.Equal(3, doc.BitFlags[0].BitFlags!.Length);
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

        var serializer = new XmlSerializer(typeof(AvTypesDocument));
        using var reader = new StringReader(xml);
        var doc = serializer.Deserialize(reader) as AvTypesDocument;

        Assert.NotNull(doc);
        Assert.NotNull(doc!.Structs);
        Assert.Equal(2, doc.Structs.Length);
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

        var serializer = new XmlSerializer(typeof(DBusNode));
        using var reader = new StringReader(xml);
        var node = serializer.Deserialize(reader) as DBusNode;

        Assert.NotNull(node);
        Assert.NotNull(node!.Interfaces);
        Assert.Single(node.Interfaces);

        var iface = node.Interfaces[0];
        Assert.Equal("org.test.Simple", iface.Name);
        Assert.Single(iface.Methods!);
        Assert.True(iface.Methods != null, "iface.Methods != null");
        Assert.Equal("Ping", iface.Methods[0].Name);
        Assert.Single(iface.Properties!);
        Assert.True(iface.Properties != null, "iface.Properties != null");
        Assert.Equal("Status", iface.Properties[0].Name);
        Assert.Equal("s", iface.Properties[0].Type);
        Assert.Equal("read", iface.Properties[0].Access);
        Assert.Single(iface.Signals!);
        Assert.True(iface.Signals != null, "iface.Signals != null");
        Assert.Equal("StatusChanged", iface.Signals[0].Name);
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

        var serializer = new XmlSerializer(typeof(DBusNode));
        using var reader = new StringReader(xml);
        var node = serializer.Deserialize(reader) as DBusNode;

        Assert.NotNull(node);
        Assert.True(node.Interfaces != null, "node.Interfaces != null");
        var method = node.Interfaces[0].Methods?[0];
        Assert.True(method != null, nameof(method) + " != null");
        Assert.Equal("Transfer", method.Name);
        Assert.Equal(3, method.Arguments!.Length);
        Assert.Equal("in", method.Arguments[0].Direction);
        Assert.Equal("source", method.Arguments[0].Name);
        Assert.Equal("s", method.Arguments[0].Type);
        Assert.Equal("out", method.Arguments[2].Direction);
    }
}
