namespace Avalonia.DBus.SourceGen.Tests;

public class DiagnosticEmissionTests
{
    // ── ADBUS001 ─────────────────────────────────────────────────────────────

    [Fact]
    public void MalformedXml_EmitsADbus001ContainingFileName()
    {
        var (result, _) = GeneratorTestHelper.RunGenerator("not xml at all <><>", "Proxy");

        var diag = Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001");
        Assert.Contains("TestInterface.xml", diag.GetMessage());
    }

    [Fact]
    public void WrongRootElement_EmitsADbus001ContainingFileName()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <not-a-node>
              <interface name="org.test.X">
                <method name="Test"/>
              </interface>
            </not-a-node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        var diag = Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001");
        Assert.Contains("TestInterface.xml", diag.GetMessage());
    }

    [Fact]
    public void DtdDeclarationInXml_EmitsADbus001()
    {
        // DTD processing is prohibited in the generator's XmlReaderSettings.
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE node SYSTEM "dbus.dtd">
            <node>
              <interface name="org.test.X">
                <method name="Test"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001");
    }

    [Fact]
    public void MissingInterfaceName_EmitsADbus001()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface>
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        var diag = Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001");
        Assert.True(diag.GetMessage().Contains("missing required 'name'", StringComparison.OrdinalIgnoreCase));
    }

    // ── ADBUS002 ─────────────────────────────────────────────────────────────

    private static (GeneratorDriverRunResult, Compilation) RunWithBadFlagValue(string flagName, string flagValue)
    {
        var xml = $"""
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

        var typesXml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="{flagName}" Value="{flagValue}"/>
              </BitFlags>
            </Types>
            """;

        return GeneratorTestHelper.RunGenerator(
            additionalTexts:
            [
                ("/test/DBusXml/TestInterface.xml", xml, (string?)"Handler"),
                ("/test/DBusXml/types.xml", typesXml, (string?)null)
            ]);
    }

    [Fact]
    public void NonNumericFlagValue_EmitsADbus002WithFlagNameAndValue()
    {
        var (result, outputCompilation) = RunWithBadFlagValue("Evil", "not_a_number");

        var diag = Assert.Single(result.Diagnostics, d => d.Id == "ADBUS002");
        var message = diag.GetMessage();
        Assert.Contains("Evil", message);
        Assert.Contains("not_a_number", message);

        // Generator must still produce compilable output (defaults bad value to 0)
        Assert.Empty(outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void FloatFlagValue_EmitsADbus002()
    {
        var (result, _) = RunWithBadFlagValue("FloatFlag", "1.5");

        var diag = Assert.Single(result.Diagnostics, d => d.Id == "ADBUS002");
        Assert.Contains("FloatFlag", diag.GetMessage());
        Assert.Contains("1.5", diag.GetMessage());
    }

    [Fact]
    public void NegativeFlagValue_DoesNotEmitADbus002()
    {
        // Negative integers are valid (long.TryParse succeeds with NumberStyles.Integer)
        var (result, _) = RunWithBadFlagValue("AllBitsSet", "-1");

        Assert.Empty(result.Diagnostics.Where(d => d.Id == "ADBUS002"));
    }

    private static (GeneratorDriverRunResult, Compilation) RunWithTypesXml(string typesXml)
    {
        const string xml = """
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

        return GeneratorTestHelper.RunGenerator(
            additionalTexts:
            [
                ("/test/DBusXml/TestInterface.xml", xml, (string?)"Handler"),
                ("/test/DBusXml/types.xml", typesXml, (string?)null)
            ]);
    }

    [Fact]
    public void MultipleBadFlagValues_EmitsOneADbus002PerBadValue()
    {
        var typesXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="Good"  Value="1"/>
                <BitFlag Name="Bad1"  Value="nope"/>
                <BitFlag Name="Bad2"  Value="also_bad"/>
                <BitFlag Name="Bad3"  Value="3.14"/>
              </BitFlags>
            </Types>
            """;

        var (result, outputCompilation) = RunWithTypesXml(typesXml);

        Assert.Equal(3, result.Diagnostics.Count(d => d.Id == "ADBUS002"));
        Assert.Empty(outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void MixedValidAndInvalidFlagValues_OnlyInvalidEmitsADbus002()
    {
        var typesXml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://avaloniaui.net/dbus/1.0">
              <BitFlags Name="TestFlags">
                <BitFlag Name="Read"    Value="1"/>
                <BitFlag Name="Write"   Value="0x02"/>
                <BitFlag Name="Execute" Value="4"/>
                <BitFlag Name="Bad"     Value="rw"/>
              </BitFlags>
            </Types>
            """;

        var (result, _) = RunWithTypesXml(typesXml);

        Assert.Equal(1, result.Diagnostics.Count(d => d.Id == "ADBUS002"));
    }
}
