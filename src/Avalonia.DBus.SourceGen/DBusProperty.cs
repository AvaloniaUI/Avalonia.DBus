using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusProperty : DBusValue
{
    [XmlAttribute("access")]
    public string? Access { get; set; }
}