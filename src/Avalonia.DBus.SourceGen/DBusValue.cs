namespace Avalonia.DBus.SourceGen;

public class DBusValue
{
    private DBusSourceGenerator.DBusDotnetType? _dbusDotnetType;

    public string? Name { get; set; }

    public string? SafeName { get; set; }

    public string? Type { get; set; }

    public AvTypeDefinition? TypeDefinition { get; set; }

    public DBusSourceGenerator.DBusDotnetType DBusDotnetType => _dbusDotnetType ??= DBusSourceGenerator.DBusDotnetType.FromDBusValue(this);

    internal void ApplyStructAliases(IReadOnlyDictionary<string, string> aliasBySignature)
    {
        if (string.IsNullOrWhiteSpace(Type))
            return;

        var type = _dbusDotnetType ?? DBusSourceGenerator.DBusDotnetType.FromDBusValue(this);
        _dbusDotnetType = type.ApplyStructAliases(aliasBySignature);
    }
}
