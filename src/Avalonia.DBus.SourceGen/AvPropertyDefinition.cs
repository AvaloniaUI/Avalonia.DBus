namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvPropertyDefinition : AvTypeContainer
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlAttribute("Type")]
    public string? Type { get; set; }
}