using System;
using System.ComponentModel;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

internal static class AvDbusXml
{
    public const string Namespace = "http://avaloniaui.net/dbus/1.0";
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvTypeDefinition : AvTypeContainer
{
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvTypeContainer
{
    [XmlElement("Struct", Namespace = AvDbusXml.Namespace)]
    public AvStructType? Struct { get; set; }

    [XmlElement("List", Namespace = AvDbusXml.Namespace)]
    public AvListType? List { get; set; }

    [XmlElement("Dictionary", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryType? Dictionary { get; set; }

    [XmlElement("BitFlags", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagsType? BitFlags { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvStructType
{
    [XmlAttribute("Type")]
    public string? Type { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvListType : AvTypeContainer
{
}

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

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvDictionaryKey : AvTypeContainer
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvDictionaryValue : AvTypeContainer
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagsType
{
    [XmlAttribute("Type")]
    public string? Type { get; set; }
}

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

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvPropertyDefinition : AvTypeContainer
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlAttribute("Type")]
    public string? Type { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvDictionaryDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlElement("Key", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryKey? Key { get; set; }

    [XmlElement("Value", Namespace = AvDbusXml.Namespace)]
    public AvDictionaryValue? Value { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagsDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlElement("BitFlag", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagDefinition[]? BitFlags { get; set; }

    [XmlElement("Flag", Namespace = AvDbusXml.Namespace)]
    public AvBitFlagDefinition[]? Flags { get; set; }
}

[Serializable]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = AvDbusXml.Namespace)]
public class AvBitFlagDefinition
{
    [XmlAttribute("Name")]
    public string? Name { get; set; }

    [XmlAttribute("Value")]
    public string? Value { get; set; }
}
