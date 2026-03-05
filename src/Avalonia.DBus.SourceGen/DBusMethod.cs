namespace Avalonia.DBus.SourceGen;

public class DBusMethod
{
    public string? Name { get; set; }

    public string? SafeName { get; set; }

    public DBusArgument[]? Arguments { get; set; }
}
