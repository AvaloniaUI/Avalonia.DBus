using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Avalonia.DBus.PublicApiExtractor;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var csprojPath = args.Length >= 1 ? args[0] : "src/Avalonia.DBus/Avalonia.DBus.csproj";
            var outputPath = args.Length >= 2 ? args[1] : "public-api.txt";
            if (args.Length > 2) return Usage();

            var compilation = CreateCompilationFromCsproj(csprojPath);

            var lines = new List<string>(capacity: 16_384);
            lines.Add($"# Public API for: {Path.GetFullPath(csprojPath)}");
            lines.Add($"# Assembly: {compilation.AssemblyName}");
            lines.Add($"# Generated: {DateTimeOffset.UtcNow:O}");
            lines.Add("");

            var typeFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

            var memberFormat = new SymbolDisplayFormat(
                globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                genericsOptions:
                    SymbolDisplayGenericsOptions.IncludeTypeParameters |
                    SymbolDisplayGenericsOptions.IncludeVariance,
                memberOptions:
                    SymbolDisplayMemberOptions.IncludeContainingType |
                    SymbolDisplayMemberOptions.IncludeParameters |
                    SymbolDisplayMemberOptions.IncludeType |
                    SymbolDisplayMemberOptions.IncludeExplicitInterface,
                parameterOptions:
                    SymbolDisplayParameterOptions.IncludeType |
                    SymbolDisplayParameterOptions.IncludeName |
                    SymbolDisplayParameterOptions.IncludeDefaultValue |
                    SymbolDisplayParameterOptions.IncludeParamsRefOut |
                    SymbolDisplayParameterOptions.IncludeExtensionThis,
                propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
                kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword,
                miscellaneousOptions:
                    SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                    SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

            var allTypes = new ConcurrentBag<INamedTypeSymbol>();
            CollectTypes(compilation.Assembly.GlobalNamespace, allTypes);

            var orderedTypes = allTypes
                .Distinct(new NamedTypeSymbolComparer())
                .OrderBy(t => t.ToDisplayString(typeFormat), StringComparer.Ordinal)
                .ToList();

            foreach (var t in orderedTypes)
            {
                if (t.IsImplicitlyDeclared) continue;
                if (!IsExternallyVisibleType(t)) continue;

                lines.Add(RenderTypeHeader(t, typeFormat));

                var members = t.GetMembers()
                    .Where(m => !m.IsImplicitlyDeclared)
                    .Where(IsExternallyVisibleMember)
                    .Select(m => RenderMember(m, memberFormat))
                    .Where(s => s.Length != 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                foreach (var m in members)
                    lines.Add($"  {m}");

                // Include explicit interface implementations (callable via the interface) even though they're often private.
                var explicitImpls = GetExplicitInterfaceImplementations(t)
                    .Select(m => RenderMember(m, memberFormat))
                    .Where(s => s.Length != 0)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                foreach (var m in explicitImpls)
                    lines.Add($"  {m}");

                lines.Add("");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
            await File.WriteAllLinesAsync(outputPath, lines, Encoding.UTF8).ConfigureAwait(false);

            Console.WriteLine($"Wrote {lines.Count} lines to {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: dotnet run --project tools/Avalonia.DBus.PublicApiExtractor -- [csprojPath] [outputPath]");
        Console.Error.WriteLine("Defaults:");
        Console.Error.WriteLine("  csprojPath: src/Avalonia.DBus/Avalonia.DBus.csproj");
        Console.Error.WriteLine("  outputPath: public-api.txt");
        return 2;
    }

    private static CSharpCompilation CreateCompilationFromCsproj(string csprojPath)
    {
        csprojPath = Path.GetFullPath(csprojPath);
        var projectDir = Path.GetDirectoryName(csprojPath) ?? ".";
        var assemblyName = Path.GetFileNameWithoutExtension(csprojPath);

        var removeGlobs = ReadCompileRemoveGlobs(csprojPath);

        var csFiles = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !IsUnderDirectory(p, "bin") && !IsUnderDirectory(p, "obj"))
            .Where(p => !MatchesAnyGlob(projectDir, p, removeGlobs))
            .ToList();

        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithKind(SourceCodeKind.Regular);

        var trees = new List<SyntaxTree>(csFiles.Count);
        foreach (var file in csFiles.OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file, Encoding.UTF8);
            trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, path: file));
        }

        var references = GetTrustedPlatformAssemblyReferences();

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: true,
            nullableContextOptions: NullableContextOptions.Enable);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: trees,
            references: references,
            options: compilationOptions);

        // Don't fail on compile errors; we only need symbol info for what Roslyn can bind.
        // Still print errors to help catch missing references if the API output looks wrong.
        var diags = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(20)
            .ToList();
        foreach (var d in diags)
            Console.Error.WriteLine($"[compile] {d.Id}: {d.GetMessage()} ({d.Location.GetLineSpan().Path})");

        return compilation;
    }

    private static IReadOnlyList<string> ReadCompileRemoveGlobs(string csprojPath)
    {
        try
        {
            var doc = XDocument.Load(csprojPath);
            var remove = doc.Descendants().Where(e => e.Name.LocalName == "Compile")
                .Select(e => (string?)e.Attribute("Remove"))
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .SelectMany(s => s!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToList();

            return remove;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsUnderDirectory(string path, string dirName)
    {
        var parts = Path.GetFullPath(path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(p => string.Equals(p, dirName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesAnyGlob(string projectDir, string filePath, IReadOnlyList<string> globs)
    {
        if (globs.Count == 0) return false;
        var rel = Path.GetRelativePath(projectDir, filePath).Replace('\\', '/');
        foreach (var g in globs)
        {
            var re = GlobToRegex(g.Replace('\\', '/'));
            if (re.IsMatch(rel))
                return true;
        }
        return false;
    }

    private static Regex GlobToRegex(string glob)
    {
        // Minimal glob support: *, ?, ** (directory wildcard). Matches against forward-slash normalized relative paths.
        var sb = new StringBuilder();
        sb.Append("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*')
            {
                var isDoubleStar = i + 1 < glob.Length && glob[i + 1] == '*';
                if (isDoubleStar)
                {
                    sb.Append(".*");
                    i++;
                }
                else
                {
                    sb.Append("[^/]*");
                }
                continue;
            }

            if (c == '?')
            {
                sb.Append("[^/]");
                continue;
            }

            sb.Append(Regex.Escape(c.ToString()));
        }
        sb.Append("$");
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static IReadOnlyList<MetadataReference> GetTrustedPlatformAssemblyReferences()
    {
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        if (string.IsNullOrWhiteSpace(tpa))
            return Array.Empty<MetadataReference>();

        return tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
    }

    private static void CollectTypes(INamespaceSymbol ns, ConcurrentBag<INamedTypeSymbol> allTypes)
    {
        foreach (var memberNs in ns.GetNamespaceMembers())
            CollectTypes(memberNs, allTypes);

        foreach (var type in ns.GetTypeMembers())
            CollectTypes(type, allTypes);
    }

    private static void CollectTypes(INamedTypeSymbol type, ConcurrentBag<INamedTypeSymbol> allTypes)
    {
        allTypes.Add(type);
        foreach (var nested in type.GetTypeMembers())
            CollectTypes(nested, allTypes);
    }

    private static bool IsExternallyVisibleType(INamedTypeSymbol t)
    {
        // A nested public type must have all containing types externally visible too.
        for (INamedTypeSymbol? cur = t; cur is not null; cur = cur.ContainingType)
        {
            if (!IsExternallyVisibleAccessibility(cur.DeclaredAccessibility))
                return false;
        }

        // Namespace types must be public to be externally visible.
        return t.ContainingType is not null || t.DeclaredAccessibility == Accessibility.Public;
    }

    private static bool IsExternallyVisibleMember(ISymbol m)
    {
        if (!IsExternallyVisibleAccessibility(m.DeclaredAccessibility))
            return false;

        // Skip synthesized accessors; properties/events will represent them.
        if (m is IMethodSymbol ms && ms.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove)
            return false;

        return true;
    }

    private static bool IsExternallyVisibleAccessibility(Accessibility a) =>
        a is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;

    private static IEnumerable<IMethodSymbol> GetExplicitInterfaceImplementations(INamedTypeSymbol t)
    {
        if (!IsExternallyVisibleType(t))
            yield break;

        foreach (var m in t.GetMembers().OfType<IMethodSymbol>())
        {
            if (m.IsImplicitlyDeclared) continue;
            if (m.ExplicitInterfaceImplementations.Length == 0) continue;
            yield return m;
        }
    }

    private static string RenderTypeHeader(INamedTypeSymbol t, SymbolDisplayFormat typeFormat)
    {
        var sb = new StringBuilder();
        sb.Append(t.TypeKind switch
        {
            TypeKind.Class => "class ",
            TypeKind.Struct => "struct ",
            TypeKind.Interface => "interface ",
            TypeKind.Enum => "enum ",
            TypeKind.Delegate => "delegate ",
            _ => "type ",
        });

        sb.Append(t.ToDisplayString(typeFormat));
        return sb.ToString();
    }

    private static string RenderMember(ISymbol m, SymbolDisplayFormat memberFormat)
    {
        if (m is IPropertySymbol ps)
            return ps.ToDisplayString(memberFormat);
        if (m is IEventSymbol es)
            return es.ToDisplayString(memberFormat);
        if (m is IFieldSymbol fs)
            return fs.ToDisplayString(memberFormat);
        if (m is IMethodSymbol mms)
            return mms.ToDisplayString(memberFormat);
        if (m is INamedTypeSymbol nts && IsExternallyVisibleType(nts))
            return $"nested {nts.TypeKind.ToString().ToLowerInvariant()} {nts.ToDisplayString(memberFormat)}";

        return "";
    }

    private sealed class NamedTypeSymbolComparer : IEqualityComparer<INamedTypeSymbol>
    {
        public bool Equals(INamedTypeSymbol? x, INamedTypeSymbol? y) =>
            SymbolEqualityComparer.Default.Equals(x, y);

        public int GetHashCode(INamedTypeSymbol obj) =>
            SymbolEqualityComparer.Default.GetHashCode(obj);
    }
}
