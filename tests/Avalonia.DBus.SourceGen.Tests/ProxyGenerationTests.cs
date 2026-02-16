using Microsoft.CodeAnalysis;
using Xunit;

namespace Avalonia.DBus.SourceGen.Tests;

public class ProxyGenerationTests
{
    [Fact]
    public void ProxyGeneration_MethodWithInOutArgs_GeneratesCorrectSignature()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Calculator">
                <method name="Add">
                  <arg direction="in" name="a" type="i"/>
                  <arg direction="in" name="b" type="i"/>
                  <arg direction="out" type="i"/>
                </method>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        // Should contain an async method for "Add"
        Assert.Contains("AddAsync", generatedSource);
        // Should reference the interface name
        Assert.Contains("org.test.Calculator", generatedSource);
    }

    [Fact]
    public void ProxyGeneration_Properties_GeneratesGetSetMethods()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WithProps">
                <property name="Name" type="s" access="read"/>
                <property name="Count" type="i" access="readwrite"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        // Read property should have a Get method
        Assert.Contains("GetNamePropertyAsync", generatedSource);
        // ReadWrite property should have both Get and Set
        Assert.Contains("GetCountPropertyAsync", generatedSource);
        Assert.Contains("SetCountPropertyAsync", generatedSource);
    }

    [Fact]
    public void ProxyGeneration_Signals_GeneratesWatchMethods()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.WithSignals">
                <signal name="Changed">
                  <arg name="newValue" type="s"/>
                </signal>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("WatchChangedAsync", generatedSource);
    }

    [Fact]
    public void ProxyGeneration_MultipleInterfaces_GeneratesAll()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.First">
                <method name="MethodA">
                  <arg direction="out" type="s"/>
                </method>
              </interface>
              <interface name="org.test.Second">
                <method name="MethodB">
                  <arg direction="out" type="i"/>
                </method>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("MethodAAsync", generatedSource);
        Assert.Contains("MethodBAsync", generatedSource);
    }

    [Fact]
    public void ProxyGeneration_VoidMethod_NoReturnType()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Void">
                <method name="DoWork">
                  <arg direction="in" name="input" type="s"/>
                </method>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Proxy");

        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("DoWorkAsync", generatedSource);
    }
}
