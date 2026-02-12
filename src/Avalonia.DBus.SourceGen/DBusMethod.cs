namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusMethod
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("arg")]
    public DBusArgument[]? Arguments { get; set; }
}