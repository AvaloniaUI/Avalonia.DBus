using System;
using System.Collections.Generic;
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

    [XmlElement("TypeDefinition", Namespace = AvDbusXml.Namespace)]
    public AvTypeDefinition? TypeDefinition { get; set; }

    [XmlIgnore]
    public DBusSourceGenerator.DBusDotnetType DBusDotnetType => _dbusDotnetType ??= DBusSourceGenerator.DBusDotnetType.FromDBusValue(this);

    internal void ApplyStructAliases(IReadOnlyDictionary<string, string> aliasBySignature)
    {
        if (string.IsNullOrWhiteSpace(Type))
            return;

        var type = _dbusDotnetType ?? DBusSourceGenerator.DBusDotnetType.FromDBusValue(this);
        _dbusDotnetType = type.ApplyStructAliases(aliasBySignature);
    }
}
