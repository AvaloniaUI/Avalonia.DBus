namespace Avalonia.DBus.SourceGen.Tests;

internal sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
{
    public override string Path => path;

    public override SourceText? GetText(CancellationToken cancellationToken = default)
        => SourceText.From(text);
}