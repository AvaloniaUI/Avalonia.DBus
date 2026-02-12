namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvTypeContainer
{
    [XmlElement("Struct", Namespace = AvDbusXml.Namespace)]
    public AvStructType? Struct { get; set; }

    [XmlElement("List", Namespace = AvDbusXml.Namespace)]
    public AvListType? List { get; set; }

    [XmlElement("Dictionary", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryType? Dictionary { get; set; }

    [XmlElement("BitFlags", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagsType? BitFlags { get; set; }
}