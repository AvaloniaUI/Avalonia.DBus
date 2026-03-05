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
        // Hint names now use SafeName (OrgTest_evil____), so no CS8785 is emitted and source is generated.
        Assert.Empty(result.Diagnostics.Where(d => d.Id == "CS8785"));
        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("OrgTest_evil____", generatedSource);
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
        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("foo_bar__", generatedSource);
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
        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("Foo_bar__", generatedSource);
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
        Assert.Single(result.Diagnostics, d => d.Id == "ADBUS002");
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

    [Fact]
    public void CSharpKeyword_AsArgumentName_EscapedWithAt()
    {
        // Argument names are Camelized. "class" → Camelize → "class" → C# keyword → "@class".
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Keywords">
                <method name="Send">
                  <arg direction="in" name="class" type="s"/>
                  <arg direction="in" name="event" type="s"/>
                </method>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("@class", generatedSource);
        Assert.Contains("@event", generatedSource);
    }

    [Fact]
    public void NumericStartingName_PrefixedWithUnderscore()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Numeric">
                <method name="123abc"/>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        Assert.Contains("_123abc", generatedSource);
    }

    [Fact]
    public void WriteonlyProperty_GeneratesSetOnlyAccessor()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WriteOnly">
                <property name="Sink" type="s" access="write"/>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(outputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
        // write-only interface property should declare set; but not get;
        Assert.Contains("Sink { set; }", generatedSource);
        Assert.DoesNotContain("Sink { get;", generatedSource);
    }
}
