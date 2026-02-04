using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true)]
public class DBusValue
{
    [XmlIgnore]
    private DBusSourceGenerator.DBusDotnetType? _dbusDotnetType;

    [XmlAttribute("name")]
    public string? Name { get; set; }

    [XmlAttribute("type")]
    public string? Type { get; set; }

    [XmlIgnore]
    public DBusSourceGenerator.DBusDotnetType DBusDotnetType => _dbusDotnetType ??= DBusSourceGenerator.DBusDotnetType.FromDBusValue(this);
}