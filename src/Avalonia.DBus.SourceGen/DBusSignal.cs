namespace Avalonia.DBus.SourceGen;

public class DBusSignal
{
    public string? Name { get; set; }

    public string? SafeName { get; set; }

    public DBusArgument[]? Arguments { get; set; }
}
