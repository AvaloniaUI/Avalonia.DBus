namespace Avalonia.DBus.SourceGen;

[Generator]
public partial class DBusSourceGenerator : IIncrementalGenerator
{
    private const string PrivateImplementationNamespace = "Avalonia.DBus.SourceGen.PrivateImplementationDoNoTouch";

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

        IncrementalValuesProvider<(DBusNode, string, string, string)> generatorProvider = context.AdditionalTextsProvider
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

                    if (dBusNode.Interfaces is null)
                        return default;

                    x.Right.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir);
                    x.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                    var userFacingNamespace = GetUserFacingNamespace(x.Left.Path, projectDir, rootNamespace);
                    return ValueTuple.Create(dBusNode, generatorMode, x.Left.Path, userFacingNamespace);
                }
                catch
                {
                    return default;
                }
            })
            .Where(static x => x is { Item1: not null, Item2: not null, Item3: not null, Item4: not null });

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

            var interfaceContexts = provider
                .SelectMany(value => value.Item1.Interfaces!.Select(dBusInterface => (Interface: dBusInterface, UserFacingNamespace: value.Item4)))
                .ToArray();
            var dBusInterfaces = interfaceContexts.Select(static context => context.Interface).ToArray();
            var structAliases = CollectStructAliases(dBusInterfaces);
            ApplyStructAliases(dBusInterfaces, structAliases);

            var structMetadata = LoadStructDefinitions(importPaths, xmlByPath, typesSerializer, xmlReaderSettings);
            foreach (var group in interfaceContexts.GroupBy(static context => context.UserFacingNamespace, StringComparer.Ordinal))
            {
                var structDefinitions = CollectStructDefinitions(group.Select(static context => context.Interface), structMetadata);
                if (structDefinitions.Count == 0)
                    continue;

                productionContext.AddSource(
                    $"{GetHintPrefix(group.Key)}.DBusStructs.g.cs",
                    BuildStructsSource(structDefinitions.Values, group.Key));
            }

            var bitFlagDefinitions = LoadBitFlagsDefinitions(importPaths, xmlByPath, typesSerializer, xmlReaderSettings);
            foreach (var group in interfaceContexts.GroupBy(static context => context.UserFacingNamespace, StringComparer.Ordinal))
            {
                var (dictionaryAliases, bitFlagsAliases) = CollectTypeAliases(group.Select(static context => context.Interface));
                var aliasesSource = BuildTypeAliasesSource(dictionaryAliases, bitFlagsAliases, bitFlagDefinitions, group.Key);
                if (!string.IsNullOrWhiteSpace(aliasesSource))
                {
                    productionContext.AddSource($"{GetHintPrefix(group.Key)}.DBusTypeAliases.g.cs", aliasesSource);
                }
            }

            var proxyRegistrations = new Dictionary<string, ProxyRegistration>(StringComparer.Ordinal);
            var handlerRegistrations = new Dictionary<string, HandlerRegistration>(StringComparer.Ordinal);
            foreach ((DBusNode Node, string GeneratorMode, string Path, string UserFacingNamespace) value in provider)
            {
                switch (value.GeneratorMode)
                {
                    case "Proxy":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface);
                            var namespaceDeclaration = NamespaceDeclaration(
                                    ParseName(value.UserFacingNamespace))
                                .AddMembers(typeDeclarationSyntax);
                            var compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.UserFacingNamespace)}.{Pascalize(dBusInterface.Name.AsSpan())}Proxy.g.cs",
                                compilationUnit.GetText(Encoding.UTF8));
                            var proxyIdentifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Proxy";
                            var proxyTypeName = GetGlobalQualifiedTypeName(value.UserFacingNamespace, proxyIdentifier);
                            proxyRegistrations[proxyTypeName] = new ProxyRegistration(proxyTypeName, dBusInterface.Name!);
                        }

                        break;
                    case "Handler":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            var source = BuildHandlerSource(dBusInterface, value.UserFacingNamespace);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.UserFacingNamespace)}.{Pascalize(dBusInterface.Name.AsSpan())}Handler.g.cs",
                                source);
                            var handlerHelperIdentifier = GetHandlerRegistrationHelperIdentifier(dBusInterface);
                            var handlerInterfaceIdentifier = GetHandlerInterfaceIdentifier(dBusInterface);
                            var helperTypeName = GetGlobalQualifiedTypeName(PrivateImplementationNamespace, handlerHelperIdentifier);
                            var interfaceTypeName = GetGlobalQualifiedTypeName(value.UserFacingNamespace, handlerInterfaceIdentifier);
                            handlerRegistrations[helperTypeName] = new HandlerRegistration(interfaceTypeName, helperTypeName);
                        }

                        break;
                }
            }

            if (proxyRegistrations.Count > 0 || handlerRegistrations.Count > 0)
            {
                var metadataSource = BuildGeneratedPrivateImplementationSource(
                    proxyRegistrations.Values,
                    handlerRegistrations.Values);
                if (!string.IsNullOrWhiteSpace(metadataSource))
                {
                    productionContext.AddSource(
                        "Avalonia.DBus.SourceGen.GeneratedPrivateImplementationDoNotTouch.Metadata.g.cs",
                        metadataSource);
                }
            }

        });
    }
}
