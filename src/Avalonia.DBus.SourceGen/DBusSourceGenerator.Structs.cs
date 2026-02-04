using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private sealed class StructDefinition
    {
        public StructDefinition(string signature, DBusDotnetType[] fields)
        {
            Signature = signature;
            Fields = fields;
        }

        public string Signature { get; }

        public DBusDotnetType[] Fields { get; }
    }

    private static IReadOnlyDictionary<string, StructDefinition> CollectStructDefinitions(IEnumerable<DBusInterface> interfaces)
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

                        CollectStructDefinitions(arg.DBusDotnetType, definitions);
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

                        CollectStructDefinitions(arg.DBusDotnetType, definitions);
                    }
                }
            }

            if (dBusInterface.Properties is not null)
            {
                foreach (var prop in dBusInterface.Properties)
                {
                    if (string.IsNullOrWhiteSpace(prop.Type))
                        continue;

                    CollectStructDefinitions(prop.DBusDotnetType, definitions);
                }
            }
        }

        return definitions;
    }

    private static void CollectStructDefinitions(DBusDotnetType type, IDictionary<string, StructDefinition> definitions)
    {
        if (type.DotnetType == DotnetType.Struct && !string.IsNullOrEmpty(type.DBusTypeSignature))
        {
            if (!definitions.ContainsKey(type.DBusTypeSignature))
            {
                definitions[type.DBusTypeSignature] = new StructDefinition(type.DBusTypeSignature, type.InnerTypes);
            }
        }
 
        foreach (var inner in type.InnerTypes)
        {
            CollectStructDefinitions(inner, definitions);
        } 
    }

    private static string GetStructTypeName(string signature)
        => $"DbusStruct_{Pascalize(SanitizeSignature(signature).AsSpan())}";

    private static bool ContainsStruct(DBusDotnetType type)
    {
        if (type.DotnetType == DotnetType.Struct)
            return true;

        if (type.InnerTypes is null)
            return false;

        foreach (var inner in type.InnerTypes)
        {
            if (ContainsStruct(inner))
                return true;
        }

        return false;
    }

    private static string GetTypeName(DBusDotnetType type) => type.ToTypeSyntax().ToString();

    private static string GetRawTypeName(DBusDotnetType type)
    {
        return type.DotnetType switch
        {
            DotnetType.Struct => "DBusStruct",
            DotnetType.Array => $"List<{GetRawTypeName(type.InnerTypes[0])}>",
            DotnetType.Dictionary => $"Dictionary<{GetRawTypeName(type.InnerTypes[0])}, {GetRawTypeName(type.InnerTypes[1])}>",
            _ => type.ToTypeSyntax().ToString()
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
            DotnetType.Struct => $"{GetStructTypeName(type.DBusTypeSignature)}.FromDbusStruct((DBusStruct){source})",
            DotnetType.Array => MakeFromDbusArrayExpressionString(type, source),
            DotnetType.Dictionary => MakeFromDbusDictExpressionString(type, source),
            _ => $"({GetTypeName(type)}){source}"
        };
    }

    private static string MakeFromDbusArrayExpressionString(DBusDotnetType type, string source)
    {
        var elementType = type.InnerTypes[0];
        if (!ContainsStruct(elementType))
            return $"({GetTypeName(type)}){source}";

        string rawElementType = GetRawTypeName(elementType);
        string rawArrayType = $"List<{rawElementType}>";
        string strongElementType = GetTypeName(elementType);
        string strongArrayType = $"List<{strongElementType}>";
        string itemVar = "item";
        string convertedItem = MakeFromDbusValueExpressionString(elementType, itemVar);

        return $"new {strongArrayType}((({rawArrayType}){source}).Select({itemVar} => {convertedItem}))";
    }

    private static string MakeFromDbusDictExpressionString(DBusDotnetType type, string source)
    {
        var keyType = type.InnerTypes[0];
        var valueType = type.InnerTypes[1];
        if (!ContainsStruct(keyType) && !ContainsStruct(valueType))
            return $"({GetTypeName(type)}){source}";

        string rawKeyType = GetRawTypeName(keyType);
        string rawValueType = GetRawTypeName(valueType);
        string rawDictType = $"Dictionary<{rawKeyType}, {rawValueType}>";
        string strongKeyType = GetTypeName(keyType);
        string strongValueType = GetTypeName(valueType);
        string strongDictType = $"Dictionary<{strongKeyType}, {strongValueType}>";

        string keyExpr = MakeFromDbusValueExpressionString(keyType, "kv.Key");
        string valueExpr = MakeFromDbusValueExpressionString(valueType, "kv.Value");

        return $"new {strongDictType}((({rawDictType}){source}).Select(kv => new KeyValuePair<{strongKeyType}, {strongValueType}>({keyExpr}, {valueExpr})))";
    }

    private static string MakeToDbusValueExpressionString(DBusDotnetType type, string source)
    {
        return type.DotnetType switch
        {
            DotnetType.Struct => $"{source}.ToDbusStruct()",
            _ => source
        };
    }

    private static string BuildStructsSource(IEnumerable<StructDefinition> definitions)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Avalonia.DBus;");
        sb.AppendLine();
        sb.AppendLine("#pragma warning disable");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("namespace Avalonia.DBus.SourceGen");
        sb.AppendLine("{");

        foreach (var definition in definitions.OrderBy(static d => d.Signature, StringComparer.Ordinal))
        {
            string typeName = GetStructTypeName(definition.Signature);
            string signatureLiteral = SymbolDisplay.FormatLiteral(definition.Signature, true);

            string parameters = string.Join(", ", definition.Fields.Select((field, index) =>
                $"{GetTypeName(field)} Item{index + 1}"));

            sb.AppendLine($"    internal sealed record {typeName}({parameters})");
            sb.AppendLine("    {");
            sb.AppendLine($"        public const string Signature = {signatureLiteral};");
            sb.AppendLine();
            sb.AppendLine($"        public static {typeName} FromDbusStruct(DBusStruct value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                throw new ArgumentNullException(nameof(value));");

            string fromFields = string.Join(", ", definition.Fields.Select((field, index) =>
                MakeFromDbusValueExpressionString(field, $"value[{index}]") ));

            sb.AppendLine($"            return new {typeName}({fromFields});");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public DBusStruct ToDbusStruct()");
            sb.AppendLine("        {");

            string toFields = string.Join(", ", definition.Fields.Select((field, index) =>
                MakeToDbusValueExpressionString(field, $"Item{index + 1}") ));

            sb.AppendLine($"            return new DBusStruct({toFields});");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        public static List<{typeName}> FromDbusArray(List<DBusStruct> value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("                throw new ArgumentNullException(nameof(value));");
            sb.AppendLine($"            return new List<{typeName}>(value.Select(static item => FromDbusStruct(item)));" );
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
}
