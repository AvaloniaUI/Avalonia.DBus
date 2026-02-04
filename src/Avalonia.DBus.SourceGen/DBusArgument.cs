using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusArgument : DBusValue
{
    [XmlAttribute("direction")]
    public string? Direction { get; set; }
}