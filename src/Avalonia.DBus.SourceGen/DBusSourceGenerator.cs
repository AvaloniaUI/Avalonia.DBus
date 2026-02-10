using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Avalonia.DBus.SourceGen;

[Generator]
public partial class DBusSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        XmlSerializer xmlSerializer = new(typeof(DBusNode));
        XmlSerializer typesSerializer = new(typeof(AvTypesDocument));
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

        context.RegisterPostInitializationOutput(initializationContext =>
        {
            initializationContext.AddSource("Avalonia.DBus.SourceGen.PropertyChanges.cs", PropertyChangesClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.DBusObjectTree.cs", DBusObjectTreeClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.DBusObject.cs", DBusObjectClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.IDBusObject.cs", IDBusObjectClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.DBusBuiltIns.cs", DBusBuiltInsClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.DBusConnectionObjectExtensions.cs", DBusConnectionObjectExtensionsClass);
        });

        IncrementalValuesProvider<(DBusNode, string, string)> generatorProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((x, _) =>
            {
                if (!x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.DBusGeneratorMode", out var generatorMode))
                    return default;
                try
                {
                    if (xmlSerializer.Deserialize(XmlReader.Create(new StringReader(x.Left.GetText()!.ToString()), xmlReaderSettings)) is not DBusNode dBusNode)
                        return default;

                    return dBusNode.Interfaces is null ? default : ValueTuple.Create(dBusNode, generatorMode, x.Left.Path);
                }
                catch
                {
                    return default;
                }
            })
            .Where(static x => x is { Item1: not null, Item2: not null, Item3: not null });

        var xmlTextProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Select((text, _) => (text.Path, Text: text.GetText()?.ToString()))
            .Where(static x => x.Text is not null);

        var combinedProvider = generatorProvider.Collect().Combine(xmlTextProvider.Collect());

        context.RegisterSourceOutput(combinedProvider, (productionContext, data) =>
        {
            var provider = data.Left;
            if (provider.IsEmpty)
                return;

            var xmlByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in data.Right
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.Text)))
            {
                xmlByPath[entry.Path] = entry.Text!;
            }

            var importPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in provider)
            {
                var node = value.Item1;
                if (node.ImportTypes is null || node.ImportTypes.Length == 0)
                    continue;

                var baseDirectory = Path.GetDirectoryName(value.Item3) ?? string.Empty;
                foreach (var import in node.ImportTypes)
                {
                    if (string.IsNullOrWhiteSpace(import))
                        continue;

                    var resolvedPath = Path.GetFullPath(Path.Combine(baseDirectory, import));
                    importPaths.Add(resolvedPath);
                }
            }

            var interfaces = provider.Select(static value => value.Item1).SelectMany(static node => node.Interfaces!);
            var dBusInterfaces = interfaces as DBusInterface[] ?? interfaces.ToArray();
            var structAliases = CollectStructAliases(dBusInterfaces);
            ApplyStructAliases(dBusInterfaces, structAliases);

            var structMetadata = LoadStructDefinitions(importPaths, xmlByPath, typesSerializer, xmlReaderSettings);
            var structDefinitions = CollectStructDefinitions(dBusInterfaces, structMetadata);
            if (structDefinitions.Count > 0)
            {
                productionContext.AddSource(
                    "Avalonia.DBus.SourceGen.DBusStructs.g.cs",
                    BuildStructsSource(structDefinitions.Values));
            }

            var (dictionaryAliases, bitFlagsAliases) = CollectTypeAliases(dBusInterfaces);

            if (dictionaryAliases.Count > 0 || bitFlagsAliases.Count > 0)
            {
                var bitFlagDefinitions = LoadBitFlagsDefinitions(importPaths, xmlByPath, typesSerializer, xmlReaderSettings);
                var aliasesSource = BuildTypeAliasesSource(dictionaryAliases, bitFlagsAliases, bitFlagDefinitions);
                if (!string.IsNullOrWhiteSpace(aliasesSource))
                {
                    productionContext.AddSource("Avalonia.DBus.SourceGen.DBusTypeAliases.g.cs", aliasesSource);
                }
            }

            foreach ((DBusNode Node, string GeneratorMode, string Path) value in provider)
            {
                switch (value.GeneratorMode)
                {
                    case "Proxy":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface);
                            var namespaceDeclaration = NamespaceDeclaration(
                                    IdentifierName("Avalonia.DBus.SourceGen"))
                                .AddMembers(typeDeclarationSyntax);
                            var compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                            productionContext.AddSource($"Avalonia.DBus.SourceGen.{Pascalize(dBusInterface.Name.AsSpan())}Proxy.g.cs", compilationUnit.GetText(Encoding.UTF8));
                        }

                        break;
                    case "Handler":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            TypeDeclarationSyntax typeDeclarationSyntax = GenerateHandler(dBusInterface);
                            var namespaceDeclaration = NamespaceDeclaration(
                                    IdentifierName("Avalonia.DBus.SourceGen"))
                                .AddMembers(typeDeclarationSyntax);
                            var compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                            productionContext.AddSource($"Avalonia.DBus.SourceGen.{Pascalize(dBusInterface.Name.AsSpan())}Handler.g.cs", compilationUnit.GetText(Encoding.UTF8));
                        }

                        break;
                }
            }

        });
    }
}
