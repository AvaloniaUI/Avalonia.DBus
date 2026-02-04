using System;
using System.IO;
using System.Text;
using System.Linq;
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
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreWhitespace = true,
            IgnoreComments = true
        };

        context.RegisterPostInitializationOutput(initializationContext =>
        {
            initializationContext.AddSource("Avalonia.DBus.SourceGen.PropertyChanges.cs", PropertyChangesClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.PathHandler.cs", PathHandlerClass);
            initializationContext.AddSource("Avalonia.DBus.SourceGen.IDBusInterfaceHandler.cs", DBusInterfaceHandlerInterface);
        });

        IncrementalValuesProvider<(DBusNode, string)> generatorProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((x, _) =>
            {
                if (!x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.DBusGeneratorMode", out var generatorMode))
                    return default;
                if (xmlSerializer.Deserialize(XmlReader.Create(new StringReader(x.Left.GetText()!.ToString()), xmlReaderSettings)) is not DBusNode dBusNode)
                    return default;
                return dBusNode.Interfaces is null ? default : ValueTuple.Create(dBusNode, generatorMode);
            })
            .Where(static x => x is { Item1: not null, Item2: not null });

        context.RegisterSourceOutput(generatorProvider.Collect(), (productionContext, provider) =>
        {
            if (provider.IsEmpty)
                return;

            var structDefinitions = CollectStructDefinitions(provider.Select(static value => value.Item1).SelectMany(static node => node.Interfaces!));
            if (structDefinitions.Count > 0)
            {
                productionContext.AddSource(
                    "Avalonia.DBus.SourceGen.DBusStructs.g.cs",
                    BuildStructsSource(structDefinitions.Values));
            }

            foreach ((DBusNode Node, string GeneratorMode) value in provider)
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
