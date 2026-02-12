namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvDictionaryDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlElement("Key", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryKey? Key { get; set; }

    [XmlElement("Value", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryValue? Value { get; set; }
}