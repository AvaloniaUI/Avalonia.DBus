using Microsoft.CodeAnalysis.Diagnostics;

namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
{
    public static readonly TestAnalyzerConfigOptions Empty = new();

    public override bool TryGetValue(string key, out string value)
    {
        value = null!;
        return false;
    }
}