namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlAttribute("Value")]
    public string? Value { get; set; }
}