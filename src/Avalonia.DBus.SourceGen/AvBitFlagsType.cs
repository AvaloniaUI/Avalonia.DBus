namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagsType
{
    [XmlAttribute("Type")]
    public string? Type { get; set; }
}