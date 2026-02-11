using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvStructDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlElement("Property", Namespace = AvDbusXml.Namespace)]
    public AvPropertyDefinition[]? Properties { get; set; }
}