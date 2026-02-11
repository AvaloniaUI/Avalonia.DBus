using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvStructType
{
    [XmlAttribute("Type")]
    public string? Type { get; set; }
}