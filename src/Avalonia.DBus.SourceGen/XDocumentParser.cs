namespace Avalonia.DBus.SourceGen;

internal static class XDocumentParser
{
    private static readonly XNamespace Av = AvDbusXml.Namespace;

    public static DBusNode ParseNode(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");

        if (root.Name.LocalName != "node")
            throw new InvalidOperationException($"Expected root element 'node' but found '{root.Name.LocalName}'.");

        var importTypes = root.Elements(Av + "ImportTypes")
            .Select(e => e.Value)
            .ToArray();

        var interfaces = root.Elements("interface")
            .Select(ParseInterface)
            .ToArray();

        return new DBusNode
        {
            ImportTypes = NullIfEmpty(importTypes),
            Interfaces = NullIfEmpty(interfaces)
        };
    }

    public static AvTypesDocument ParseTypesDocument(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("XML document has no root element.");

        var structs = root.Elements(Av + "Struct")
            .Select(ParseStructDefinition)
            .ToArray();

        var dictionaries = root.Elements(Av + "Dictionary")
            .Select(ParseDictionaryDefinition)
            .ToArray();

        var bitFlags = root.Elements(Av + "BitFlags")
            .Select(ParseBitFlagsDefinition)
            .ToArray();

        return new AvTypesDocument
        {
            Structs = NullIfEmpty(structs),
            Dictionaries = NullIfEmpty(dictionaries),
            BitFlags = NullIfEmpty(bitFlags)
        };
    }

    private static DBusInterface ParseInterface(XElement el)
    {
        var name = (string?)el.Attribute("name");
        return new DBusInterface
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Methods = NullIfEmpty(el.Elements("method").Select(ParseMethod).ToArray()),
            Signals = NullIfEmpty(el.Elements("signal").Select(ParseSignal).ToArray()),
            Properties = NullIfEmpty(el.Elements("property").Select(ParseProperty).ToArray())
        };
    }

    private static DBusMethod ParseMethod(XElement el)
    {
        var name = (string?)el.Attribute("name");
        return new DBusMethod
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Arguments = NullIfEmpty(el.Elements("arg").Select(ParseArgument).ToArray())
        };
    }

    private static DBusSignal ParseSignal(XElement el)
    {
        var name = (string?)el.Attribute("name");
        return new DBusSignal
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Arguments = NullIfEmpty(el.Elements("arg").Select(ParseArgument).ToArray())
        };
    }

    private static DBusProperty ParseProperty(XElement el)
    {
        var name = (string?)el.Attribute("name");
        return new DBusProperty
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Type = (string?)el.Attribute("type"),
            Access = (string?)el.Attribute("access"),
            TypeDefinition = ParseTypeDefinition(el)
        };
    }

    private static DBusArgument ParseArgument(XElement el)
    {
        var name = (string?)el.Attribute("name");
        return new DBusArgument
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Type = (string?)el.Attribute("type"),
            Direction = (string?)el.Attribute("direction"),
            TypeDefinition = ParseTypeDefinition(el)
        };
    }

    private static AvTypeDefinition? ParseTypeDefinition(XElement parent)
    {
        var el = parent.Element(Av + "TypeDefinition");
        if (el is null)
            return null;

        var def = new AvTypeDefinition();
        PopulateTypeContainer(def, el);
        return def;
    }

    private static void PopulateTypeContainer(AvTypeContainer container, XElement el)
    {
        var structEl = el.Element(Av + "Struct");
        if (structEl is not null)
            container.Struct = new AvStructType { Type = (string?)structEl.Attribute("Type") };

        var listEl = el.Element(Av + "List");
        if (listEl is not null)
        {
            var list = new AvListType();
            PopulateTypeContainer(list, listEl);
            container.List = list;
        }

        var dictEl = el.Element(Av + "Dictionary");
        if (dictEl is not null)
            container.Dictionary = ParseDictionaryType(dictEl);

        var bitFlagsEl = el.Element(Av + "BitFlags");
        if (bitFlagsEl is not null)
            container.BitFlags = new AvBitFlagsType { Type = (string?)bitFlagsEl.Attribute("Type") };
    }

    private static AvDictionaryType ParseDictionaryType(XElement el)
    {
        var dict = new AvDictionaryType
        {
            Type = (string?)el.Attribute("Type")
        };

        var keyEl = el.Element(Av + "Key");
        if (keyEl is not null)
        {
            var keyName = (string?)keyEl.Attribute("Name");
            var key = new AvDictionaryKey
            {
                Name = keyName,
                SafeName = MakeSafeName(keyName)
            };
            PopulateTypeContainer(key, keyEl);
            dict.Key = key;
        }

        var valueEl = el.Element(Av + "Value");
        if (valueEl is not null)
        {
            var valueName = (string?)valueEl.Attribute("Name");
            var value = new AvDictionaryValue
            {
                Name = valueName,
                SafeName = MakeSafeName(valueName)
            };
            PopulateTypeContainer(value, valueEl);
            dict.Value = value;
        }

        return dict;
    }

    private static AvStructDefinition ParseStructDefinition(XElement el)
    {
        var name = (string?)el.Attribute("Name");
        return new AvStructDefinition
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Properties = NullIfEmpty(el.Elements(Av + "Property").Select(ParsePropertyDefinition).ToArray())
        };
    }

    private static AvPropertyDefinition ParsePropertyDefinition(XElement el)
    {
        var name = (string?)el.Attribute("Name");
        var def = new AvPropertyDefinition
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Type = (string?)el.Attribute("Type")
        };
        PopulateTypeContainer(def, el);
        return def;
    }

    private static AvDictionaryDefinition ParseDictionaryDefinition(XElement el)
    {
        var name = (string?)el.Attribute("Name");
        var def = new AvDictionaryDefinition
        {
            Name = name,
            SafeName = MakeSafeName(name)
        };

        var keyEl = el.Element(Av + "Key");
        if (keyEl is not null)
        {
            var keyName = (string?)keyEl.Attribute("Name");
            var key = new AvDictionaryKey
            {
                Name = keyName,
                SafeName = MakeSafeName(keyName)
            };
            PopulateTypeContainer(key, keyEl);
            def.Key = key;
        }

        var valueEl = el.Element(Av + "Value");
        if (valueEl is not null)
        {
            var valueName = (string?)valueEl.Attribute("Name");
            var value = new AvDictionaryValue
            {
                Name = valueName,
                SafeName = MakeSafeName(valueName)
            };
            PopulateTypeContainer(value, valueEl);
            def.Value = value;
        }

        return def;
    }

    private static AvBitFlagsDefinition ParseBitFlagsDefinition(XElement el)
    {
        var name = (string?)el.Attribute("Name");

        var bitFlagElements = el.Elements(Av + "BitFlag")
            .Select(ParseBitFlagDefinition)
            .ToArray();

        var flagElements = el.Elements(Av + "Flag")
            .Select(ParseBitFlagDefinition)
            .ToArray();

        return new AvBitFlagsDefinition
        {
            Name = name,
            SafeName = MakeSafeName(name),
            BitFlags = NullIfEmpty(bitFlagElements),
            Flags = NullIfEmpty(flagElements)
        };
    }

    private static AvBitFlagDefinition ParseBitFlagDefinition(XElement el)
    {
        var name = (string?)el.Attribute("Name");
        return new AvBitFlagDefinition
        {
            Name = name,
            SafeName = MakeSafeName(name),
            Value = (string?)el.Attribute("Value")
        };
    }

    private static string? MakeSafeName(string? rawName) =>
        rawName is not null ? DBusSourceGenerator.MakeSafeIdentifier(rawName) : null;

    private static T[]? NullIfEmpty<T>(T[] array) =>
        array is { Length: > 0 } ? array : null;
}
