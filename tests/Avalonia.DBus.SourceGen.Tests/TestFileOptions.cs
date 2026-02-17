namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class TestFileOptions(string generatorMode) : AnalyzerConfigOptions
{
    public override bool TryGetValue(string key, out string value)
    {
        if (key == "build_metadata.AdditionalFiles.DBusGeneratorMode")
        {
            value = generatorMode;
            return true;
        }
        value = null!;
        return false;
    }
}