namespace Avalonia.DBus.SourceGen.Tests;

public class IntrospectionGenerationTests
{
    [Fact]
    public void HandlerGeneration_EmitsWriteIntrospectionXmlMethod()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Introspectable">
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("WriteIntrospectionXml", generatedSource);
        Assert.Contains("System.Text.StringBuilder sb", generatedSource);
        Assert.Contains("string indent", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_IntrospectionContainsProperties()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WithProps">
                <property name="Enabled" type="b" access="read"/>
                <property name="Label" type="s" access="readwrite"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("<property name=", generatedSource);
        Assert.Contains("\"Enabled\"", generatedSource);
        Assert.Contains("\"b\"", generatedSource);
        Assert.Contains("\"read\"", generatedSource);
        Assert.Contains("\"Label\"", generatedSource);
        Assert.Contains("\"s\"", generatedSource);
        Assert.Contains("\"readwrite\"", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_IntrospectionContainsMethods()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WithMethods">
                <method name="Add">
                  <arg direction="in" name="a" type="i"/>
                  <arg direction="in" name="b" type="i"/>
                  <arg direction="out" type="i"/>
                </method>
                <method name="NoArgs"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("<method name=", generatedSource);
        Assert.Contains("\"Add\"", generatedSource);
        Assert.Contains("\"a\"", generatedSource);
        Assert.Contains("\"i\"", generatedSource);
        Assert.Contains("\"in\"", generatedSource);
        Assert.Contains("\"out\"", generatedSource);
        Assert.Contains("\"NoArgs\"", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_IntrospectionContainsSignals()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WithSignals">
                <signal name="Changed">
                  <arg name="new_value" type="b"/>
                </signal>
                <signal name="Closed"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("<signal name=", generatedSource);
        Assert.Contains("\"Changed\"", generatedSource);
        Assert.Contains("\"new_value\"", generatedSource);
        Assert.Contains("\"b\"", generatedSource);
        Assert.Contains("\"Closed\"", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_IntrospectionRegisteredInMetadata()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Registered">
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        // The module initializer should pass WriteIntrospectionXml to the metadata registration
        Assert.Contains("WriteIntrospectionXml =", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_IntrospectionCompilesWithoutErrors()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Full">
                <property name="Version" type="u" access="read"/>
                <property name="Name" type="s" access="readwrite"/>
                <method name="GetInfo">
                  <arg direction="in" name="id" type="i"/>
                  <arg direction="out" name="info" type="s"/>
                </method>
                <method name="Reset"/>
                <signal name="Updated">
                  <arg name="timestamp" type="x"/>
                </signal>
              </interface>
            </node>
            """;

        var (result, outputCompilation) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var compilationDiags = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationDiags);
    }
}
