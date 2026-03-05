namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly Dictionary<string, string?> _modeByPath;
    private readonly string _projectDir;
    private readonly string _rootNamespace;

    public TestAnalyzerConfigOptionsProvider(
        string xmlPath, string generatorMode, string projectDir, string rootNamespace)
    {
        _modeByPath = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [xmlPath] = generatorMode
        };
        _projectDir = projectDir;
        _rootNamespace = rootNamespace;
    }

    public TestAnalyzerConfigOptionsProvider(
        Dictionary<string, string?> modeByPath, string projectDir, string rootNamespace)
    {
        _modeByPath = new Dictionary<string, string?>(modeByPath, StringComparer.OrdinalIgnoreCase);
        _projectDir = projectDir;
        _rootNamespace = rootNamespace;
    }

    public override AnalyzerConfigOptions GlobalOptions =>
        new TestGlobalOptions(_projectDir, _rootNamespace);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
        TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
    {
        if (_modeByPath.TryGetValue(textFile.Path, out var mode) && mode != null)
            return new TestFileOptions(mode);
        return TestAnalyzerConfigOptions.Empty;
    }
}
