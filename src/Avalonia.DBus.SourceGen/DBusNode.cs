using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false, ElementName = "node")]
public class DBusNode
{
    [XmlElement("interface")]
    public DBusInterface[]? Interfaces { get; set; }
}