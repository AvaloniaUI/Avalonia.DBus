using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private sealed class StructDefinition(string name, string signature, DBusDotnetType[] fields, string[]? fieldNames)
    {
        public string Name { get; } = name;

        public string Signature { get; } = signature;

        public DBusDotnetType[] Fields { get; } = fields;

        public string[]? FieldNames { get; } = fieldNames;
    }

    private static IReadOnlyDictionary<string, StructDefinition> CollectStructDefinitions(
        IEnumerable<DBusInterface> interfaces,
        IReadOnlyDictionary<string, AvStructDefinition> structDefinitions)
    {
        Dictionary<string, StructDefinition> definitions = new();

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

                        CollectStructDefinitions(arg.DBusDotnetType, definitions, structDefinitions);
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

                        CollectStructDefinitions(arg.DBusDotnetType, definitions, structDefinitions);
                    }
                }
            }

            if (dBusInterface.Properties is not null)
            {
                foreach (var prop in dBusInterface.Properties)
                {
                    if (string.IsNullOrWhiteSpace(prop.Type))
                        continue;

                    CollectStructDefinitions(prop.DBusDotnetType, definitions, structDefinitions);
                }
            }
        }

        return definitions;
    }

    private static void CollectStructDefinitions(
        DBusDotnetType type,
        IDictionary<string, StructDefinition> definitions,
        IReadOnlyDictionary<string, AvStructDefinition> structDefinitions)
    {
        if (type.DotnetType == DotnetType.Struct && !string.IsNullOrEmpty(type.DBusTypeSignature))
        {
            var typeName = string.IsNullOrWhiteSpace(type.AliasName)
                ? GetStructTypeName(type.DBusTypeSignature)
                : type.AliasName!;

            if (!definitions.ContainsKey(typeName))
            {
                var fieldNames = TryGetStructFieldNames(typeName, type.InnerTypes.Length, structDefinitions);
                definitions[typeName] = new StructDefinition(typeName, type.DBusTypeSignature, type.InnerTypes, fieldNames);
            }
        }

        foreach (var inner in type.InnerTypes)
        {
            CollectStructDefinitions(inner, definitions, structDefinitions);
        }
    }

    private static string GetStructTypeName(string signature)
        => $"DbusStruct_{Pascalize(SanitizeSignature(signature).AsSpan())}";

    private static bool RequiresFromDbusConversion(DBusDotnetType type)
    {
        if (type.DotnetType == DotnetType.Struct || type.IsBitFlags)
            return true;

        foreach (var inner in type.InnerTypes)
        {
            if (RequiresFromDbusConversion(inner))
                return true;
        }

        return false;
    }

    private static bool RequiresToDbusConversion(DBusDotnetType type)
    {
        if (type.DotnetType == DotnetType.Struct)
            return false;

        if (type.IsBitFlags)
            return true;

        foreach (var inner in type.InnerTypes)
        {
            if (RequiresToDbusConversion(inner))
                return true;
        }

        return false;
    }

    private static string GetTypeName(DBusDotnetType type) => type.ToTypeSyntax().ToString();

    private static string GetUnderlyingTypeName(DBusDotnetType type)
    {
        return type.DotnetType switch
        {
            DotnetType.Byte => "byte",
            DotnetType.Bool => "bool",
            DotnetType.Int16 => "short",
            DotnetType.UInt16 => "ushort",
            DotnetType.Int32 => "int",
            DotnetType.UInt32 => "uint",
            DotnetType.Int64 => "long",
            DotnetType.UInt64 => "ulong",
            DotnetType.Double => "double",
            DotnetType.String => "string",
            DotnetType.ObjectPath => "DBusObjectPath",
            DotnetType.Signature => "DBusSignature",
            DotnetType.Variant => "DBusVariant",
            DotnetType.UnixFd => "DBusUnixFd",
            DotnetType.Struct => "DBusStruct",
            DotnetType.Array => $"List<{GetUnderlyingTypeName(type.InnerTypes[0])}>",
            DotnetType.Dictionary => $"Dictionary<{GetUnderlyingTypeName(type.InnerTypes[0])}, {GetUnderlyingTypeName(type.InnerTypes[1])}>",
            _ => type.ToTypeSyntax().ToString()
        };
    }

    private static string GetRawTypeName(DBusDotnetType type)
    {
        return type.DotnetType switch
        {
            DotnetType.Array => $"List<{GetRawTypeName(type.InnerTypes[0])}>",
            DotnetType.Dictionary => $"Dictionary<{GetRawTypeName(type.InnerTypes[0])}, {GetRawTypeName(type.InnerTypes[1])}>",
            _ => GetUnderlyingTypeName(type)
        };
    }

    private static string GetToDbusTypeName(DBusDotnetType type)
    {
        if (type.IsBitFlags)
            return GetUnderlyingTypeName(type);

        return type.DotnetType switch
        {
            DotnetType.Array => $"List<{GetToDbusTypeName(type.InnerTypes[0])}>",
            DotnetType.Dictionary => $"Dictionary<{GetToDbusTypeName(type.InnerTypes[0])}, {GetToDbusTypeName(type.InnerTypes[1])}>",
            _ => GetTypeName(type)
        };
    }

    private static ExpressionSyntax MakeFromDbusValueExpression(DBusDotnetType type, ExpressionSyntax source)
        => ParseExpression(MakeFromDbusValueExpressionString(type, source.ToString()));

    private static ExpressionSyntax MakeToDbusValueExpression(DBusDotnetType type, ExpressionSyntax source)
        => ParseExpression(MakeToDbusValueExpressionString(type, source.ToString()));

    private static string MakeFromDbusValueExpressionString(DBusDotnetType type, string source)
    {
        return type.DotnetType switch
        {
            DotnetType.Struct => $"{GetTypeName(type)}.FromDbusStruct((DBusStruct){source})",
            DotnetType.Array => MakeFromDbusArrayExpressionString(type, source),
            DotnetType.Dictionary => MakeFromDbusDictExpressionString(type, source),
            _ => $"({GetTypeName(type)}){source}"
        };
    }

    private static string MakeFromDbusArrayExpressionString(DBusDotnetType type, string source)
    {
        var elementType = type.InnerTypes[0];
        if (!RequiresFromDbusConversion(elementType))
            return $"({GetTypeName(type)}){source}";

        var rawElementType = GetRawTypeName(elementType);
        var rawArrayType = $"List<{rawElementType}>";
        var strongElementType = GetTypeName(elementType);
        var strongArrayType = $"List<{strongElementType}>";
        var itemVar = "item";
        var convertedItem = MakeFromDbusValueExpressionString(elementType, itemVar);

        return $"new {strongArrayType}((({rawArrayType}){source}).Select({itemVar} => {convertedItem}))";
    }

    private static string MakeFromDbusDictExpressionString(DBusDotnetType type, string source)
    {
        var keyType = type.InnerTypes[0];
        var valueType = type.InnerTypes[1];
        var rawKeyType = GetRawTypeName(keyType);
        var rawValueType = GetRawTypeName(valueType);
        var rawDictType = $"Dictionary<{rawKeyType}, {rawValueType}>";

        if (!RequiresFromDbusConversion(keyType) && !RequiresFromDbusConversion(valueType))
        {
            if (string.IsNullOrWhiteSpace(type.AliasName))
                return $"({GetTypeName(type)}){source}";

            return $"new {GetTypeName(type)}(({rawDictType}){source})";
        }

        var strongKeyType = GetTypeName(keyType);
        var strongValueType = GetTypeName(valueType);
        var strongDictType = $"Dictionary<{strongKeyType}, {strongValueType}>";

        var keyExpr = MakeFromDbusValueExpressionString(keyType, "kv.Key");
        var valueExpr = MakeFromDbusValueExpressionString(valueType, "kv.Value");

        var dictExpression = $"new {strongDictType}((({rawDictType}){source}).Select(kv => new KeyValuePair<{strongKeyType}, {strongValueType}>({keyExpr}, {valueExpr})))";

        if (string.IsNullOrWhiteSpace(type.AliasName))
            return dictExpression;

        return $"new {GetTypeName(type)}({dictExpression})";
    }

    private static string MakeToDbusValueExpressionString(DBusDotnetType type, string source)
    {
        return type.DotnetType switch
        {
            DotnetType.Struct => $"{source}.ToDbusStruct()",
            DotnetType.Array => MakeToDbusArrayExpressionString(type, source),
            DotnetType.Dictionary => MakeToDbusDictExpressionString(type, source),
            _ when type.IsBitFlags => $"({GetUnderlyingTypeName(type)}){source}",
            _ => source
        };
    }

    private static string MakeToDbusArrayExpressionString(DBusDotnetType type, string source)
    {
        var elementType = type.InnerTypes[0];
        if (!RequiresToDbusConversion(elementType))
            return source;

        var rawElementType = GetToDbusTypeName(elementType);
        var rawArrayType = $"List<{rawElementType}>";
        var convertedItem = MakeToDbusValueExpressionString(elementType, "item");

        return $"new {rawArrayType}(({source}).Select(item => {convertedItem}))";
    }

    private static string MakeToDbusDictExpressionString(DBusDotnetType type, string source)
    {
        var keyType = type.InnerTypes[0];
        var valueType = type.InnerTypes[1];
        if (!RequiresToDbusConversion(keyType) && !RequiresToDbusConversion(valueType))
            return source;

        var rawKeyType = GetToDbusTypeName(keyType);
        var rawValueType = GetToDbusTypeName(valueType);
        var rawDictType = $"Dictionary<{rawKeyType}, {rawValueType}>";

        var keyExpr = MakeToDbusValueExpressionString(keyType, "kv.Key");
        var valueExpr = MakeToDbusValueExpressionString(valueType, "kv.Value");

        return $"new {rawDictType}(({source}).Select(kv => new KeyValuePair<{rawKeyType}, {rawValueType}>({keyExpr}, {valueExpr})))";
    }

    private static string BuildStructsSource(IEnumerable<StructDefinition> definitions, string userFacingNamespace)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Avalonia.DBus;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {userFacingNamespace}");
        sb.AppendLine("{");

        foreach (var definition in definitions.OrderBy(static d => d.Name, StringComparer.Ordinal))
        {
            var typeName = SanitizeIdentifier(definition.Name);
            var signatureLiteral = SymbolDisplay.FormatLiteral(definition.Signature, true);

            var parameters = string.Join(", ", definition.Fields.Select((field, index) =>
                $"{GetTypeName(field)} {GetStructFieldName(definition, index)}"));

            sb.AppendLine($"    public sealed record {typeName}({parameters})");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string Signature = {signatureLiteral};");
            sb.AppendLine();
            sb.AppendLine($"        public static {typeName} FromDbusStruct(DBusStruct value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                throw new ArgumentNullException(nameof(value));");

            var fromFields = string.Join(", ", definition.Fields.Select((field, index) =>
                MakeFromDbusValueExpressionString(field, $"value[{index}]")));

            sb.AppendLine($"            return new {typeName}({fromFields});");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public DBusStruct ToDbusStruct()");
            sb.AppendLine("        {");

            var toFields = string.Join(", ", definition.Fields.Select((field, index) =>
                MakeToDbusValueExpressionString(field, GetStructFieldName(definition, index))));

            sb.AppendLine($"            return new DBusStruct({toFields});");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static List<{typeName}> FromDbusArray(List<DBusStruct> value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                throw new ArgumentNullException(nameof(value));");
            sb.AppendLine($"            return new List<{typeName}>(value.Select(static item => FromDbusStruct(item)));");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static List<DBusStruct> ToDbusArray(List<{typeName}> value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                throw new ArgumentNullException(nameof(value));");
            sb.AppendLine("            return new List<DBusStruct>(value.Select(static item => item.ToDbusStruct()));");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GetStructFieldName(StructDefinition definition, int index)
    {
        if (definition.FieldNames is { Length: > 0 } names && index < names.Length && !string.IsNullOrWhiteSpace(names[index]))
            return names[index];

        return $"Item{index + 1}";
    }

    private static string[]? TryGetStructFieldNames(
        string structName,
        int fieldCount,
        IReadOnlyDictionary<string, AvStructDefinition> structDefinitions)
    {
        if (fieldCount <= 0 || structDefinitions.Count == 0)
            return null;

        if (!structDefinitions.TryGetValue(structName, out var definition))
            return null;

        if (definition.Properties is null || definition.Properties.Length != fieldCount)
            return null;

        var used = new HashSet<string>(StringComparer.Ordinal);
        var fieldNames = new string[fieldCount];
        for (var i = 0; i < fieldCount; i++)
        {
            var name = definition.Properties[i]?.Name;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            var identifier = SanitizeIdentifier(Pascalize(name.AsSpan()));
            if (!used.Add(identifier))
                return null;

            fieldNames[i] = identifier;
        }

        return fieldNames;
    }
}
