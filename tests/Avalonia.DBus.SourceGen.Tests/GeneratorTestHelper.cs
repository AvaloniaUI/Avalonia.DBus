namespace Avalonia.DBus.SourceGen.Tests;

/// <summary>
/// Helper for running the DBusSourceGenerator against in-memory compilations.
/// </summary>
internal static class GeneratorTestHelper
{
    internal static (GeneratorDriverRunResult Result, Compilation OutputCompilation) RunGenerator(
        string xmlContent,
        string generatorMode,
        string xmlFileName = "/test/DBusXml/TestInterface.xml",
        string? additionalSource = null)
    {
        var syntaxTrees = new List<SyntaxTree>();
        if (additionalSource != null)
        {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(additionalSource));
        }

        // Add a minimal program to make the compilation valid
        syntaxTrees.Add(CSharpSyntaxTree.ParseText("namespace TestProject { }"));

        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();

        // Also add the Avalonia.DBus assembly reference
        var dbusAssembly = typeof(DBusObjectPath).Assembly;
        if (!string.IsNullOrWhiteSpace(dbusAssembly.Location))
            references.Add(MetadataReference.CreateFromFile(dbusAssembly.Location));

        var compilation = CSharpCompilation.Create(
            "TestProject",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DBusSourceGenerator();
        var additionalText = new InMemoryAdditionalText(xmlFileName, xmlContent);
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            xmlFileName, generatorMode, "/test/", "TestProject");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: [additionalText],
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);
        var result = driver.GetRunResult();

        return (result, outputCompilation);
    }
}