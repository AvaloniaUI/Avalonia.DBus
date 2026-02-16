using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class TestAnalyzerConfigOptionsProvider(
    string xmlPath,
    string generatorMode,
    string projectDir,
    string rootNamespace) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions =>
        new TestGlobalOptions(projectDir, rootNamespace);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        if (string.Equals(textFile.Path, xmlPath, StringComparison.OrdinalIgnoreCase))
            return new TestFileOptions(generatorMode);
        return TestAnalyzerConfigOptions.Empty;
    }
}