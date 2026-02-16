namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private static string GetUserFacingNamespace(string xmlPath, string? projectDir, string? rootNamespace)
    {
        var xmlDirectory = Path.GetDirectoryName(xmlPath);
        if (string.IsNullOrWhiteSpace(xmlDirectory))
            return NormalizeNamespace(rootNamespace, "Generated");

        var relativeDirectory = string.IsNullOrWhiteSpace(projectDir)
            ? Path.GetFileName(xmlDirectory)
            : MakeRelativePath(projectDir!, xmlDirectory);

        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
            return NormalizeNamespace(rootNamespace, "Generated");

        var segments = relativeDirectory
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeNamespaceSegment)
            .Where(static segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        var directoryNamespace = segments.Length == 0 ? "Generated" : string.Join(".", segments);
        return NormalizeNamespace(rootNamespace, directoryNamespace);
    }

    private static string GetHintPrefix(string ns)
        => string.Join("_", ns.Split('.').Select(SanitizeNamespaceSegment));

    private static string GetGlobalQualifiedTypeName(string ns, string typeName)
        => $"global::{ns}.{typeName}";

    private static string MakeRelativePath(string basePath, string targetPath)
    {
        try
        {
            var normalizedBasePath = EnsureTrailingSeparator(basePath);
            var normalizedTargetPath = EnsureTrailingSeparator(targetPath);
            var baseUri = new Uri(normalizedBasePath, UriKind.Absolute);
            var targetUri = new Uri(normalizedTargetPath, UriKind.Absolute);
            var relativeUri = baseUri.MakeRelativeUri(targetUri);
            if (relativeUri.IsAbsoluteUri)
                return Path.GetFileName(targetPath) ?? string.Empty;

            return Uri.UnescapeDataString(relativeUri.ToString())
                .Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            return Path.GetFileName(targetPath) ?? string.Empty;
        }
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var lastIndex = path.Length - 1;
        if (path[lastIndex] == Path.DirectorySeparatorChar || path[lastIndex] == Path.AltDirectorySeparatorChar)
            return path;

        return path + Path.DirectorySeparatorChar;
    }

    private static string SanitizeNamespaceSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return string.Empty;

        var filtered = new string(segment.Select(static ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray());
        if (string.IsNullOrWhiteSpace(filtered))
            return "_";

        if (!char.IsLetter(filtered[0]) && filtered[0] != '_')
            filtered = "_" + filtered;

        var keywordKind = SyntaxFacts.GetKeywordKind(filtered);
        var contextualKind = SyntaxFacts.GetContextualKeywordKind(filtered);
        return keywordKind != SyntaxKind.None || contextualKind != SyntaxKind.None
            ? filtered + "_"
            : filtered;
    }

    private static string NormalizeNamespace(string? rootNamespace, string suffixNamespace)
    {
        var rootSegments = string.IsNullOrWhiteSpace(rootNamespace)
            ? []
            : rootNamespace!
                .Split('.')
                .Select(SanitizeNamespaceSegment)
                .Where(static segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

        var suffixSegments = string.IsNullOrWhiteSpace(suffixNamespace)
            ? []
            : suffixNamespace
                .Split('.')
                .Select(SanitizeNamespaceSegment)
                .Where(static segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

        var allSegments = rootSegments.Concat(suffixSegments).ToArray();
        return allSegments.Length == 0 ? "Generated" : string.Join(".", allSegments);
    }

    private static CompilationUnitSyntax MakeCompilationUnit(NamespaceDeclarationSyntax namespaceDeclaration) =>
        CompilationUnit()
            .AddUsings(
                UsingDirective(
                    IdentifierName("System")),
                UsingDirective(
                    IdentifierName("System.Collections.Generic")),
                UsingDirective(
                    IdentifierName("System.Linq")),
                UsingDirective(
                    IdentifierName("System.Threading")),
                UsingDirective(
                    IdentifierName("System.Threading.Tasks")),
                UsingDirective(
                    IdentifierName("System.Xml")),
                UsingDirective(
                    IdentifierName("Avalonia.DBus")),
                UsingDirective(
                    ParseName(PrivateImplementationNamespace)))
            .WithLeadingTrivia(
                Comment("// <auto-generated>"))
            .AddMembers(namespaceDeclaration
                .WithLeadingTrivia(
                    TriviaList(
                        Trivia(
                            PragmaWarningDirectiveTrivia(
                                Token(SyntaxKind.DisableKeyword), true)),
                        Trivia(
                            NullableDirectiveTrivia(
                                Token(SyntaxKind.EnableKeyword), true)))))
            .NormalizeWhitespace();

    private static FieldDeclarationSyntax MakePrivateStringConst(string identifier, string value, TypeSyntax type) =>
        FieldDeclaration(
                VariableDeclaration(type)
                    .AddVariables(
                        VariableDeclarator(identifier)
                            .WithInitializer(
                                EqualsValueClause(
                                    MakeLiteralExpression(value)))))
            .AddModifiers(
                Token(SyntaxKind.PrivateKeyword),
                Token(SyntaxKind.ConstKeyword));

    private static FieldDeclarationSyntax MakePrivateReadOnlyField(string identifier, TypeSyntax type) =>
        FieldDeclaration(
                VariableDeclaration(type)
                    .AddVariables(
                        VariableDeclarator(identifier)))
            .AddModifiers(
                Token(SyntaxKind.PrivateKeyword),
                Token(SyntaxKind.ReadOnlyKeyword));

    private static PropertyDeclarationSyntax MakeGetOnlyProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
        PropertyDeclaration(type, identifier)
            .AddModifiers(modifiers)
            .AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));

    private static PropertyDeclarationSyntax MakeGetSetProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
        PropertyDeclaration(type, identifier)
            .AddModifiers(modifiers)
            .AddAccessorListAccessors(
                AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)),
                AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));

    private static ExpressionStatementSyntax MakeAssignmentExpressionStatement(string left, string right) =>
        ExpressionStatement(
            MakeAssignmentExpression(
                IdentifierName(left),
                IdentifierName(right)));

    private static AssignmentExpressionSyntax MakeAssignmentExpression(ExpressionSyntax left, ExpressionSyntax right) => AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);

    private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string right) => MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(left), IdentifierName(right));

    private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string middle, string right) =>
        MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, MakeMemberAccessExpression(left, middle), IdentifierName(right));

    private static LiteralExpressionSyntax MakeLiteralExpression(string literal) => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal));
}
