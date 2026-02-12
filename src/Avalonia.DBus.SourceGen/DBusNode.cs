namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false, ElementName = "node")]
public class DBusNode
{
    [XmlElement("ImportTypes", Namespace = AvDbusXml.Namespace)]
    public string[]? ImportTypes { get; set; }

    [XmlElement("interface")]
    public DBusInterface[]? Interfaces { get; set; }
}
