using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
[XmlRoot(ElementName = "Types", Namespace = AvDbusXml.Namespace, IsNullable = false)]
public class AvTypesDocument
{
    [XmlElement("Struct", Namespace = AvDbusXml.Namespace)]
    public AvStructDefinition[]? Structs { get; set; }

    [XmlElement("Dictionary", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryDefinition[]? Dictionaries { get; set; }

    [XmlElement("BitFlags", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagsDefinition[]? BitFlags { get; set; }
}