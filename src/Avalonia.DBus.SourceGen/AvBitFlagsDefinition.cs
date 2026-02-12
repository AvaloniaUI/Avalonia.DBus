namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagsDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlElement("BitFlag", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagDefinition[]? BitFlags { get; set; }

    [XmlElement("Flag", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagDefinition[]? Flags { get; set; }
}