using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
[XmlRoot(Namespace = "", IsNullable = false, ElementName = "interface")]
public class DBusInterface
{
    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlElement("method")]
    public DBusMethod[]? Methods { get; set; }

    [XmlElement("signal")]
    public DBusSignal[]? Signals { get; set; }

    [XmlElement("property")]
    public DBusProperty[]? Properties { get; set; }
}