using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private static (Dictionary<string, DBusDotnetType> DictionaryAliases, Dictionary<string, DBusDotnetType> BitFlagsAliases) CollectTypeAliases(IEnumerable<DBusInterface> interfaces)
    {
        Dictionary<string, DBusDotnetType> dictionaryAliases = new(StringComparer.Ordinal);
        Dictionary<string, DBusDotnetType> bitFlagsAliases = new(StringComparer.Ordinal);

        foreach (var dBusInterface in interfaces)
        {
            if (dBusInterface.Methods is not null)
            {
                foreach (var method in dBusInterface.Methods)
                {
                    if (method.Arguments is null)
                        continue;

                    foreach (var arg in method.Arguments)
                    {
                        if (string.IsNullOrWhiteSpace(arg.Type))
                            continue;

                        CollectTypeAliases(arg.DBusDotnetType, dictionaryAliases, bitFlagsAliases);
                    }
                }
            }

            if (dBusInterface.Signals is not null)
            {
                foreach (var signal in dBusInterface.Signals)
                {
                    if (signal.Arguments is null)
                        continue;

                    foreach (var arg in signal.Arguments)
                    {
                        if (string.IsNullOrWhiteSpace(arg.Type))
                            continue;

                        CollectTypeAliases(arg.DBusDotnetType, dictionaryAliases, bitFlagsAliases);
                    }
                }
            }

            if (dBusInterface.Properties is not null)
            {
                foreach (var prop in dBusInterface.Properties)
                {
                    if (string.IsNullOrWhiteSpace(prop.Type))
                        continue;

                    CollectTypeAliases(prop.DBusDotnetType, dictionaryAliases, bitFlagsAliases);
                }
            }
        }

        return (dictionaryAliases, bitFlagsAliases);
    }

    private static void CollectTypeAliases(DBusDotnetType type, IDictionary<string, DBusDotnetType> dictionaryAliases, IDictionary<string, DBusDotnetType> bitFlagsAliases)
    {
        var aliasName = type.AliasName;
        if (!string.IsNullOrWhiteSpace(aliasName))
        {
            var nonNullAlias = aliasName!;
            if (type.DotnetType == DotnetType.Dictionary)
            {
                if (!dictionaryAliases.ContainsKey(nonNullAlias))
                    dictionaryAliases[nonNullAlias] = type;
            }
            else if (type.IsBitFlags)
            {
                if (!bitFlagsAliases.ContainsKey(nonNullAlias))
                    bitFlagsAliases[nonNullAlias] = type;
            }
        }

        foreach (var inner in type.InnerTypes)
        {
            CollectTypeAliases(inner, dictionaryAliases, bitFlagsAliases);
        }
    }

    private static Dictionary<string, string> CollectStructAliases(IEnumerable<DBusInterface> interfaces)
    {
        Dictionary<string, string> aliases = new(StringComparer.Ordinal);

        foreach (var dBusInterface in interfaces)
        {
            if (dBusInterface.Methods is not null)
            {
                foreach (var method in dBusInterface.Methods)
                {
                    if (method.Arguments is null)
                        continue;

                    foreach (var arg in method.Arguments)
                    {
                        if (string.IsNullOrWhiteSpace(arg.Type))
                            continue;

                        CollectStructAliases(arg.DBusDotnetType, aliases);
                    }
                }
            }

            if (dBusInterface.Signals is not null)
            {
                foreach (var signal in dBusInterface.Signals)
                {
                    if (signal.Arguments is null)
                        continue;

                    foreach (var arg in signal.Arguments)
                    {
                        if (string.IsNullOrWhiteSpace(arg.Type))
                            continue;

                        CollectStructAliases(arg.DBusDotnetType, aliases);
                    }
                }
            }

            if (dBusInterface.Properties is not null)
            {
                foreach (var prop in dBusInterface.Properties)
                {
                    if (string.IsNullOrWhiteSpace(prop.Type))
                        continue;

                    CollectStructAliases(prop.DBusDotnetType, aliases);
                }
            }
        }

        return aliases;
    }

    private static void CollectStructAliases(DBusDotnetType type, IDictionary<string, string> aliases)
    {
        if (type.DotnetType == DotnetType.Struct && !string.IsNullOrWhiteSpace(type.AliasName))
        {
            var aliasName = type.AliasName!;
            if (!aliases.ContainsKey(type.DBusTypeSignature))
                aliases[type.DBusTypeSignature] = aliasName;
        }

        foreach (var inner in type.InnerTypes)
        {
            CollectStructAliases(inner, aliases);
        }
    }

    private static void ApplyStructAliases(IEnumerable<DBusInterface> interfaces, IReadOnlyDictionary<string, string> aliases)
    {
        if (aliases.Count == 0)
            return;

        foreach (var dBusInterface in interfaces)
        {
            if (dBusInterface.Methods is not null)
            {
                foreach (var method in dBusInterface.Methods)
                {
                    if (method.Arguments is null)
                        continue;

                    foreach (var arg in method.Arguments)
                    {
                        arg.ApplyStructAliases(aliases);
                    }
                }
            }

            if (dBusInterface.Signals is not null)
            {
                foreach (var signal in dBusInterface.Signals)
                {
                    if (signal.Arguments is null)
                        continue;

                    foreach (var arg in signal.Arguments)
                    {
                        arg.ApplyStructAliases(aliases);
                    }
                }
            }

            if (dBusInterface.Properties is not null)
            {
                foreach (var prop in dBusInterface.Properties)
                {
                    prop.ApplyStructAliases(aliases);
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, AvBitFlagsDefinition> LoadBitFlagsDefinitions(
        IEnumerable<string> importPaths,
        IReadOnlyDictionary<string, string> xmlByPath,
        XmlSerializer serializer,
        XmlReaderSettings readerSettings)
    {
        Dictionary<string, AvBitFlagsDefinition> definitions = new(StringComparer.Ordinal);

        foreach (var path in importPaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !xmlByPath.TryGetValue(path, out var xmlText) || string.IsNullOrWhiteSpace(xmlText))
                continue;

            try
            {
                using var reader = XmlReader.Create(new StringReader(xmlText), readerSettings);
                if (serializer.Deserialize(reader) is not AvTypesDocument document)
                    continue;

                if (document.BitFlags is null)
                    continue;

                foreach (var bitFlags in document.BitFlags)
                {
                    if (string.IsNullOrWhiteSpace(bitFlags.Name))
                        continue;

                    definitions[bitFlags.Name!] = bitFlags;
                }
            }
            catch
            {
                // Ignore malformed metadata files to avoid breaking generation.
            }
        }

        return definitions;
    }

    private static string BuildTypeAliasesSource(
        IReadOnlyDictionary<string, DBusDotnetType> dictionaryAliases,
        IReadOnlyDictionary<string, DBusDotnetType> bitFlagsAliases,
        IReadOnlyDictionary<string, AvBitFlagsDefinition> bitFlagDefinitions)
    {
        if (dictionaryAliases.Count == 0 && bitFlagsAliases.Count == 0)
            return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Avalonia.DBus;");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Avalonia.DBus.SourceGen");
        sb.AppendLine("{");

        foreach (var alias in dictionaryAliases.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            var aliasName = SanitizeIdentifier(alias.Key);
            var keyType = GetTypeName(alias.Value.InnerTypes[0]);
            var valueType = GetTypeName(alias.Value.InnerTypes[1]);
            sb.AppendLine($"    internal sealed class {aliasName} : Dictionary<{keyType}, {valueType}>");
            sb.AppendLine("    {");
            sb.AppendLine($"        public {aliasName}()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public {aliasName}(IDictionary<{keyType}, {valueType}> entries)");
            sb.AppendLine("            : base(entries)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        foreach (var alias in bitFlagsAliases.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            var aliasName = SanitizeIdentifier(alias.Key);
            var underlyingType = GetUnderlyingTypeName(alias.Value);
            sb.AppendLine("    [Flags]");
            sb.AppendLine($"    internal enum {aliasName} : {underlyingType}");
            sb.AppendLine("    {");

            if (bitFlagDefinitions.TryGetValue(alias.Key, out var definition))
            {
                var flags = EnumerateBitFlags(definition).Where(static flag => !string.IsNullOrWhiteSpace(flag.Name)).ToArray();
                if (flags.Length == 0)
                {
                    sb.AppendLine("        None = 0");
                }
                else
                {
                    foreach (var flag in flags)
                    {
                        var flagName = SanitizeIdentifier(flag.Name!);
                        var flagValue = FormatEnumValue(flag.Value, underlyingType);
                        sb.AppendLine($"        {flagName} = {flagValue},");
                    }
                }
            }
            else
            {
                sb.AppendLine("        None = 0");
            }

            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<AvBitFlagDefinition> EnumerateBitFlags(AvBitFlagsDefinition definition)
    {
        if (definition.BitFlags is { Length: > 0 } && definition.Flags is { Length: > 0 })
            return definition.BitFlags.Concat(definition.Flags);

        if (definition.BitFlags is { Length: > 0 })
            return definition.BitFlags;

        if (definition.Flags is { Length: > 0 })
            return definition.Flags;

        return Array.Empty<AvBitFlagDefinition>();
    }

    private static string FormatEnumValue(string? rawValue, string underlyingType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return "0";

        if (!ulong.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return rawValue!;

        return underlyingType switch
        {
            "uint" => value.ToString(CultureInfo.InvariantCulture) + "u",
            "ulong" => value.ToString(CultureInfo.InvariantCulture) + "ul",
            _ => value.ToString(CultureInfo.InvariantCulture)
        };
    }
}
