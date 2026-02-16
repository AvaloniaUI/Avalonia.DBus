using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Avalonia.DBus.SourceGen.Tests;

public class ErrorReportingTests
{
    [Fact]
    public void MalformedXml_DoesNotCrashGenerator()
    {
        var xml = "this is not xml at all <><>";

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001" && d.GetMessage().Contains("TestInterface.xml"));
    }

    [Fact]
    public void EmptyNode_ProducesNoOutput()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node/>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        // No interfaces means nothing to generate
        // The generator should gracefully handle this
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void MissingInterfaceName_HandledGracefully()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface>
                <method name="Test"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        // Should not produce fatal errors
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void WrongRootElement_HandledGracefully()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <something-else>
              <interface name="org.test">
                <method name="Test"/>
              </interface>
            </something-else>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Single(result.Diagnostics, d => d.Id == "ADBUS001" && d.GetMessage().Contains("TestInterface.xml"));
    }

    [Fact]
    public void InvalidGeneratorMode_ProducesNoOutput()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Simple">
                <method name="Test"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "InvalidMode");

        // Should not crash
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void DBusNode_MalformedXml_ThrowsOnDeserialize()
    {
        var xml = "not valid xml";
        var serializer = new XmlSerializer(typeof(DBusNode));

        Assert.ThrowsAny<Exception>(() =>
        {
            using var reader = new StringReader(xml);
            serializer.Deserialize(reader);
        });
    }

    [Fact]
    public void InterfaceWithNoMethods_HandledGracefully()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Empty"/>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }
}
