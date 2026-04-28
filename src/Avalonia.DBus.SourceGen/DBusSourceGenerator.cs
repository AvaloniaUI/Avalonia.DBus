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

    private static readonly DiagnosticDescriptor InvalidEnumValueWarning = new(
        id: "ADBUS002",
        title: "Invalid enum value in type metadata",
        messageFormat: "BitFlag '{0}' has non-numeric value '{1}' which cannot be used as an enum member value. Defaulting to 0.",
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
        XmlReaderSettings xmlReaderSettings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
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
                    var doc = XDocument.Load(XmlReader.Create(new StringReader(x.Left.GetText()!.ToString()), xmlReaderSettings));
                    var dBusNode = XDocumentParser.ParseNode(doc);

                    if (dBusNode.Interfaces is null)
                        return default;

                    var options = x.Right.GetOptions(x.Left);
                    options.TryGetValue("build_metadata.AdditionalFiles.DBusNamespace", out var explicitNamespace);
                    x.Right.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projectDir);
                    x.Right.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
                    var userFacingNamespace = string.IsNullOrWhiteSpace(explicitNamespace)
                        ? GetUserFacingNamespace(x.Left.Path, projectDir, rootNamespace)
                        : NormalizeNamespace(null, explicitNamespace!);
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

        var isInternalProvider = context.AnalyzerConfigOptionsProvider.Select((options, _) =>
        {
            options.GlobalOptions.TryGetValue("build_property.AvDBusInternal", out var value);
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        });

        var fullProvider = combinedProvider.Combine(isInternalProvider);

        context.RegisterSourceOutput(fullProvider, (productionContext, data) =>
        {
            var isInternal = data.Right;
            foreach (var entry in data.Left.Left)
            {
                if (entry.ParseError != null)
                    productionContext.ReportDiagnostic(Diagnostic.Create(InvalidXmlWarning, Location.None, entry.FilePath, entry.ParseError));
            }

            var provider = data.Left.Left
                .Where(static x => x.Node != null)
                .Select(static x => (Node: x.Node!, GeneratorMode: x.GeneratorMode!, FilePath: x.FilePath!, Namespace: x.Namespace!))
                .ToImmutableArray();
            if (provider.IsEmpty)
                return;

            var xmlByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in data.Left.Right
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

            var structMetadata = LoadStructDefinitions(importPaths, xmlByPath, xmlReaderSettings);
            var allStructRegistrations = new Dictionary<string, StructRegistration>(StringComparer.Ordinal);
            foreach (var group in interfaceContexts.GroupBy(static context => context.UserFacingNamespace, StringComparer.Ordinal))
            {
                var structDefinitions = CollectStructDefinitions(group.Select(static context => context.Interface), structMetadata);
                if (structDefinitions.Count == 0)
                    continue;

                productionContext.AddSource(
                    $"{GetHintPrefix(group.Key)}.DBusStructs.g.cs",
                    BuildStructsSource(structDefinitions.Values, group.Key, isInternal));

                foreach (var definition in structDefinitions.Values)
                {
                    var qualifiedTypeName = GetGlobalQualifiedTypeName(group.Key, SanitizeIdentifier(definition.Name));
                    var signatureLiteral = SymbolDisplay.FormatLiteral(definition.Signature, quote: true);
                    allStructRegistrations[qualifiedTypeName] = new StructRegistration(qualifiedTypeName, signatureLiteral);
                }
            }

            var bitFlagDefinitions = LoadBitFlagsDefinitions(importPaths, xmlByPath, xmlReaderSettings);
            foreach (var group in interfaceContexts.GroupBy(static context => context.UserFacingNamespace, StringComparer.Ordinal))
            {
                var (dictionaryAliases, bitFlagsAliases) = CollectTypeAliases(group.Select(static context => context.Interface));
                var diagnosticsList = new List<Diagnostic>();
                var aliasesSource = BuildTypeAliasesSource(dictionaryAliases, bitFlagsAliases, bitFlagDefinitions, group.Key, diagnosticsList, isInternal);
                foreach (var diagnostic in diagnosticsList)
                    productionContext.ReportDiagnostic(diagnostic);
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
                            if (string.IsNullOrWhiteSpace(dBusInterface.SafeName))
                            {
                                productionContext.ReportDiagnostic(Diagnostic.Create(
                                    InvalidXmlWarning,
                                    Location.None,
                                    value.FilePath,
                                    "Interface element is missing required 'name' attribute."));
                                continue;
                            }

                            TypeDeclarationSyntax typeDeclarationSyntax = GenerateProxy(dBusInterface, isInternal);
                            var namespaceDeclaration = NamespaceDeclaration(
                                    ParseName(value.Namespace))
                                .AddMembers(typeDeclarationSyntax);
                            var compilationUnit = MakeCompilationUnit(namespaceDeclaration);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.Namespace)}.{dBusInterface.SafeName}Proxy.g.cs",
                                compilationUnit.GetText(Encoding.UTF8));
                            var proxyIdentifier = $"{dBusInterface.SafeName}Proxy";
                            var proxyTypeName = GetGlobalQualifiedTypeName(value.Namespace, proxyIdentifier);
                            proxyRegistrations[proxyTypeName] = new ProxyRegistration(proxyTypeName, dBusInterface.Name!);
                        }

                        break;
                    case "Handler":
                        foreach (var dBusInterface in value.Node.Interfaces!)
                        {
                            if (string.IsNullOrWhiteSpace(dBusInterface.SafeName))
                            {
                                productionContext.ReportDiagnostic(Diagnostic.Create(
                                    InvalidXmlWarning,
                                    Location.None,
                                    value.FilePath,
                                    "Interface element is missing required 'name' attribute."));
                                continue;
                            }

                            var source = BuildHandlerSource(dBusInterface, value.Namespace, isInternal);
                            productionContext.AddSource(
                                $"{GetHintPrefix(value.Namespace)}.{dBusInterface.SafeName}Handler.g.cs",
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

            if (proxyRegistrations.Count > 0 || handlerRegistrations.Count > 0 || allStructRegistrations.Count > 0)
            {
                var metadataSource = BuildGeneratedPrivateImplementationSource(
                    proxyRegistrations.Values,
                    handlerRegistrations.Values,
                    allStructRegistrations.Values);
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
