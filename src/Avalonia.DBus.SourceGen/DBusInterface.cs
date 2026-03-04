namespace Avalonia.DBus.SourceGen;

public class DBusInterface
{
    public string? Name { get; set; }

    public string? SafeName { get; set; }

    public DBusMethod[]? Methods { get; set; }

    public DBusSignal[]? Signals { get; set; }

    public DBusProperty[]? Properties { get; set; }
}
