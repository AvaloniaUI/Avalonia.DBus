using Microsoft.CodeAnalysis;
using Xunit;

namespace Avalonia.DBus.SourceGen.Tests;

public class HandlerGenerationTests
{
    [Fact]
    public void HandlerGeneration_DispatchesByMember()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Dispatch">
                <method name="MethodA">
                  <arg direction="in" name="a" type="i"/>
                  <arg direction="out" type="i"/>
                </method>
                <method name="MethodB">
                  <arg direction="in" name="b" type="s"/>
                  <arg direction="out" type="s"/>
                </method>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        // Should dispatch based on message.Member
        Assert.Contains("message.Member", generatedSource);
        Assert.Contains("\"MethodA\"", generatedSource);
        Assert.Contains("\"MethodB\"", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_PropertiesGenerateAccessors()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.test.Props">
                <property name="Name" type="s" access="read"/>
                <property name="Value" type="i" access="readwrite"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        // Handler interface should have property accessors
        Assert.Contains("Name", generatedSource);
        Assert.Contains("Value", generatedSource);
    }

    [Fact]
    public void HandlerGeneration_InterfaceNamePreserved()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <node>
              <interface name="org.example.MyService">
                <method name="Ping"/>
              </interface>
            </node>
            """;

        var (result, _) = GeneratorTestHelper.RunGenerator(xml, "Handler");

        var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));

        Assert.Contains("org.example.MyService", generatedSource);
    }
}
