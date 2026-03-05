using System.Xml.Linq;
using Avalonia.DBus.SourceGen;

namespace Avalonia.DBus.SourceGen.Tests;

public class XDocumentParserTests
{
    [Fact]
    public void ParseNode_BasicInterface_PopulatesNameAndSafeName()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.freedesktop.DBus.Peer">
                <method name="get_machine_id">
                  <arg direction="in" name="input_value" type="s"/>
                  <arg direction="out" name="machine_id" type="s"/>
                </method>
                <signal name="name_acquired">
                  <arg name="signal_arg" type="s"/>
                </signal>
                <property name="machine_name" type="s" access="read"/>
              </interface>
            </node>
            """;

        var doc = XDocument.Parse(xml);
        var node = XDocumentParser.ParseNode(doc);

        // Interface
        Assert.NotNull(node.Interfaces);
        var iface = Assert.Single(node.Interfaces);
        Assert.Equal("org.freedesktop.DBus.Peer", iface.Name);
        Assert.Equal("OrgFreedesktopDBusPeer", iface.SafeName);

        // Method
        Assert.NotNull(iface.Methods);
        var method = Assert.Single(iface.Methods);
        Assert.Equal("get_machine_id", method.Name);
        Assert.Equal("GetMachineId", method.SafeName);

        // Method arguments
        Assert.NotNull(method.Arguments);
        Assert.Equal(2, method.Arguments.Length);
        Assert.Equal("input_value", method.Arguments[0].Name);
        Assert.Equal("InputValue", method.Arguments[0].SafeName);
        Assert.Equal("in", method.Arguments[0].Direction);
        Assert.Equal("s", method.Arguments[0].Type);
        Assert.Equal("out", method.Arguments[1].Direction);

        // Signal
        Assert.NotNull(iface.Signals);
        var signal = Assert.Single(iface.Signals);
        Assert.Equal("name_acquired", signal.Name);
        Assert.Equal("NameAcquired", signal.SafeName);
        Assert.NotNull(signal.Arguments);
        Assert.Equal("signal_arg", signal.Arguments[0].Name);
        Assert.Equal("SignalArg", signal.Arguments[0].SafeName);

        // Property
        Assert.NotNull(iface.Properties);
        var prop = Assert.Single(iface.Properties);
        Assert.Equal("machine_name", prop.Name);
        Assert.Equal("MachineName", prop.SafeName);
        Assert.Equal("s", prop.Type);
        Assert.Equal("read", prop.Access);
    }

    [Fact]
    public void ParseNode_SpecialCharsInName_SanitizedInSafeName()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test;evil(){}">
                <method name="do-something!weird"/>
              </interface>
            </node>
            """;

        var doc = XDocument.Parse(xml);
        var node = XDocumentParser.ParseNode(doc);

        var iface = Assert.Single(node.Interfaces!);
        // Special chars replaced with '_', then Pascalized
        Assert.Equal("org.test;evil(){}", iface.Name);
        Assert.Equal("OrgTest_evil____", iface.SafeName);

        var method = Assert.Single(iface.Methods!);
        Assert.Equal("do-something!weird", method.Name);
        Assert.Equal("Do_something_weird", method.SafeName);
    }

    [Fact]
    public void ParseNode_ImportTypes_Extracted()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node xmlns:av="http://avaloniaui.net/dbus/1.0">
              <av:ImportTypes>types.xml</av:ImportTypes>
              <av:ImportTypes>more-types.xml</av:ImportTypes>
              <interface name="org.test.Simple">
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var doc = XDocument.Parse(xml);
        var node = XDocumentParser.ParseNode(doc);

        Assert.NotNull(node.ImportTypes);
        Assert.Equal(2, node.ImportTypes.Length);
        Assert.Equal("types.xml", node.ImportTypes[0]);
        Assert.Equal("more-types.xml", node.ImportTypes[1]);
    }

    [Fact]
    public void ParseNode_TypeDefinition_BitFlags()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node xmlns:av="http://avaloniaui.net/dbus/1.0">
              <interface name="org.test.Flags">
                <method name="SetFlags">
                  <arg direction="in" name="flags" type="u">
                    <av:TypeDefinition>
                      <av:BitFlags Type="MyFlags"/>
                    </av:TypeDefinition>
                  </arg>
                </method>
              </interface>
            </node>
            """;

        var doc = XDocument.Parse(xml);
        var node = XDocumentParser.ParseNode(doc);

        var method = node.Interfaces![0].Methods![0];
        var arg = method.Arguments![0];
        Assert.Equal("flags", arg.Name);
        Assert.Equal("u", arg.Type);
        Assert.NotNull(arg.TypeDefinition);
        Assert.NotNull(arg.TypeDefinition.BitFlags);
        Assert.Equal("MyFlags", arg.TypeDefinition.BitFlags.Type);
    }

    [Fact]
    public void ParseTypesDocument_BitFlags_PopulatesNameAndValue()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="FlagA" Value="1"/>
                <BitFlag Name="FlagB" Value="2"/>
                <Flag Name="Combined" Value="3"/>
              </BitFlags>
            </Types>
            """;

        var doc = XDocument.Parse(xml);
        var typesDoc = XDocumentParser.ParseTypesDocument(doc);

        Assert.NotNull(typesDoc.BitFlags);
        var bf = Assert.Single(typesDoc.BitFlags);
        Assert.Equal("TestFlags", bf.Name);
        Assert.Equal("TestFlags", bf.SafeName);

        // BitFlag children
        Assert.NotNull(bf.BitFlags);
        Assert.Equal(2, bf.BitFlags.Length);
        Assert.Equal("FlagA", bf.BitFlags[0].Name);
        Assert.Equal("FlagA", bf.BitFlags[0].SafeName);
        Assert.Equal("1", bf.BitFlags[0].Value);
        Assert.Equal("FlagB", bf.BitFlags[1].Name);
        Assert.Equal("2", bf.BitFlags[1].Value);

        // Flag children
        Assert.NotNull(bf.Flags);
        var flag = Assert.Single(bf.Flags);
        Assert.Equal("Combined", flag.Name);
        Assert.Equal("Combined", flag.SafeName);
        Assert.Equal("3", flag.Value);
    }

    [Fact]
    public void ParseTypesDocument_Structs_PopulatesProperties()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <Struct Name="MyStruct">
                <Property Name="field_one" Type="s"/>
                <Property Name="field_two" Type="i"/>
              </Struct>
            </Types>
            """;

        var doc = XDocument.Parse(xml);
        var typesDoc = XDocumentParser.ParseTypesDocument(doc);

        Assert.NotNull(typesDoc.Structs);
        var structDef = Assert.Single(typesDoc.Structs);
        Assert.Equal("MyStruct", structDef.Name);
        Assert.Equal("MyStruct", structDef.SafeName);

        Assert.NotNull(structDef.Properties);
        Assert.Equal(2, structDef.Properties.Length);

        Assert.Equal("field_one", structDef.Properties[0].Name);
        Assert.Equal("FieldOne", structDef.Properties[0].SafeName);
        Assert.Equal("s", structDef.Properties[0].Type);

        Assert.Equal("field_two", structDef.Properties[1].Name);
        Assert.Equal("FieldTwo", structDef.Properties[1].SafeName);
        Assert.Equal("i", structDef.Properties[1].Type);
    }
}
