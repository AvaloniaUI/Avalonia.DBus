namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class TestGlobalOptions(string projectDir, string rootNamespace) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        switch (key)
        {
            case "build_property.ProjectDir":
                value = projectDir;
                return true;
            case "build_property.RootNamespace":
                value = rootNamespace;
                return true;
            default:
                value = null!;
                return false;
        }
    }
}