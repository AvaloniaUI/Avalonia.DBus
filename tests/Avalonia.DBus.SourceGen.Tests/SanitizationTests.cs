namespace Avalonia.DBus.SourceGen.Tests;

public class SanitizationTests
{
    [Fact]
    public void SpecialChars_InInterfaceName_ProducesValidCode()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test;evil(){}">
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }

    [Fact]
    public void SpecialChars_InMethodArgName_ProducesValidCode()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Safe">
                <method name="Do">
                  <arg direction="in" name="foo;bar()" type="s"/>
                </method>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }

    [Fact]
    public void SpecialChars_InPropertyName_ProducesValidCode()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Props">
                <property name="foo;bar()" type="s" access="read"/>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }

    [Fact]
    public void NonNumericEnumValue_ProducesWarning()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <av:ImportTypes xmlns:av="http://avaloniaui.net/dbus/1.0">types.xml</av:ImportTypes>
              <interface name="org.test.Flags">
                <method name="SetFlags">
                  <arg direction="in" name="flags" type="u">
                    <av:TypeDefinition xmlns:av="http://avaloniaui.net/dbus/1.0">
                      <av:BitFlags Type="TestFlags"/>
                    </av:TypeDefinition>
                  </arg>
                </method>
              </interface>
            </node>
            """;

        var typesXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="Good" Value="1"/>
                <BitFlag Name="Evil" Value="0; } class Injected { void Run() {"/>
              </BitFlags>
            </Types>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(
            additionalTexts: new[]
            {
                ("/test/DBusXml/TestInterface.xml", xml, (string?)"Handler"),
                ("/test/DBusXml/types.xml", typesXml, (string?)null)
            });

        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }

    [Fact]
    public void HexEnumValue_ParsedCorrectly()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <av:ImportTypes xmlns:av="http://avaloniaui.net/dbus/1.0">types.xml</av:ImportTypes>
              <interface name="org.test.HexFlags">
                <method name="SetFlags">
                  <arg direction="in" name="flags" type="u">
                    <av:TypeDefinition xmlns:av="http://avaloniaui.net/dbus/1.0">
                      <av:BitFlags Type="HexTestFlags"/>
                    </av:TypeDefinition>
                  </arg>
                </method>
              </interface>
            </node>
            """;

        var typesXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="HexTestFlags">
                <BitFlag Name="FlagA" Value="0x01"/>
                <BitFlag Name="FlagB" Value="0x10"/>
              </BitFlags>
            </Types>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(
            additionalTexts: new[]
            {
                ("/test/DBusXml/TestInterface.xml", xml, (string?)"Handler"),
                ("/test/DBusXml/types.xml", typesXml, (string?)null)
            });

        Assert.Empty(result.Diagnostics.Where(d => d.Id == "ADBUS002"));

        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }
}
