namespace Avalonia.DBus.SourceGen;

[Generator]
public partial class DBusSourceGenerator : IIncrementalGenerator
{
    private const string PrivateImplementationNamespace = "Avalonia.DBus.SourceGen.PrivateImplementationDoNoTouch";

    private static readonly DiagnosticDescriptor InvalidXmlWarning = new(
        id: "ADBUS001",
        title: "Invalid D-Bus XML",
        messageFormat: "Failed to parse D-Bus XML file '{0}': {1}",
        category: "Avalonia.DBus",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private readonly struct XmlParseResult(
        DBusNode? node,
        string? generatorMode,
        string? filePath,
        string? ns,
        string? parseError)
    {
        public readonly DBusNode? Node = node;
        public readonly string? GeneratorMode = generatorMode;
        public readonly string? FilePath = filePath;
        public readonly string? Namespace = ns;
        public readonly string? ParseError = parseError;
    }

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

        var generatorProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select((x, _) =>
            {
                if (!x.Right.GetOptions(x.Left).TryGetValue("build_metadata.AdditionalFiles.DBusGeneratorMode", out var generatorMode)
                    || string.IsNullOrEmpty(generatorMode))
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
                    return new XmlParseResult(dBusNode, generatorMode, x.Left.Path, userFacingNamespace, null);
                }
                catch (Exception ex)
                {
                    return new XmlParseResult(null, null, x.Left.Path, null, ex.Message);
                }
            })
            .Where(static x => x.Node != null || x.ParseError != null);

        var xmlTextProvider = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".xml", StringComparison.Ordinal))
            .Select((text, _) => (text.Path, Text: text.GetText()?.ToString()))
            .Where(static x => x.Text is not null);

        var combinedProvider = generatorProvider.Collect().Combine(xmlTextProvider.Collect());

        context.RegisterSourceOutput(combinedProvider, (productionContext, data) =>
        {
            foreach (var entry in data.Left)
            {
                if (entry.ParseError != null)
                    productionContext.ReportDiagnostic(Diagnostic.Create(InvalidXmlWarning, Location.None, entry.FilePath, entry.ParseError));
            }

            var provider = data.Left
                .Where(static x => x.Node != null)
                .Select(static x => (Node: x.Node!, GeneratorMode: x.GeneratorMode!, FilePath: x.FilePath!, Namespace: x.Namespace!))
                .ToImmutableArray();
            if (provider.IsEmpty)
                return;

            var xmlByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in data.Right
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.Text)))
            {
                // Normalize path separators so lookups match across platforms.
                xmlByPath[NormalizePath(entry.Path)] = entry.Text!;
            }

            var importPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in provider)
            {
                var node = value.Node;
                if (node.ImportTypes is null || node.ImportTypes.Length == 0)
                    continue;

                var baseDirectory = Path.GetDirectoryName(value.FilePath) ?? string.Empty;
                foreach (var import in node.ImportTypes)
                {
                    if (string.IsNullOrWhiteSpace(import))
                        continue;

                    var resolvedPath = NormalizePath(Path.Combine(baseDirectory, import));
                    importPaths.Add(resolvedPath);
                }
            }

            var interfaceContexts = provider
                .SelectMany(value => value.Node.Interfaces!.Select(dBusInterface => (Interface: dBusInterface, UserFacingNamespace: value.Namespace)))
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
            foreach (var value in provider)
            {
                switch (value.GeneratorMode)
                {
                    case "Proxy":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface);
                            var namespaceDeclaration = NamespaceDeclaration(
                                    ParseName(value.Namespace))
                                .AddMembers(typeDeclarationSyntax);
                            var compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.Namespace)}.{Pascalize(dBusInterface.Name.AsSpan())}Proxy.g.cs",
                                compilationUnit.GetText(Encoding.UTF8));
                            var proxyIdentifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Proxy";
                            var proxyTypeName = GetGlobalQualifiedTypeName(value.Namespace, proxyIdentifier);
                            proxyRegistrations[proxyTypeName] = new ProxyRegistration(proxyTypeName, dBusInterface.Name!);
                        }

                        break;
                    case "Handler":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            var source = BuildHandlerSource(dBusInterface, value.Namespace);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.Namespace)}.{Pascalize(dBusInterface.Name.AsSpan())}Handler.g.cs",
                                source);
                            var handlerHelperIdentifier = GetHandlerRegistrationHelperIdentifier(dBusInterface);
                            var handlerInterfaceIdentifier = GetHandlerInterfaceIdentifier(dBusInterface);
                            var helperTypeName = GetGlobalQualifiedTypeName(PrivateImplementationNamespace, handlerHelperIdentifier);
                            var interfaceTypeName = GetGlobalQualifiedTypeName(value.Namespace, handlerInterfaceIdentifier);
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

    private static string NormalizePath(string path) => Path.GetFullPath(path);
}
