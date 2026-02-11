using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvDictionaryType
{
    [XmlAttribute("Type")]
    public string? Type { get; set; }

    [XmlElement("Key", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryKey? Key { get; set; }

    [XmlElement("Value", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryValue? Value { get; set; }
}