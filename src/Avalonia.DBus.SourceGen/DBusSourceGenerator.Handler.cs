using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private ClassDeclarationSyntax GenerateHandler(DBusInterface dBusInterface)
    {
        var identifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Handler";

        var cl = ClassDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.InternalKeyword),
                Token(SyntaxKind.AbstractKeyword))
            .AddBaseListTypes(
                SimpleBaseType(
                    IdentifierName("IDBusInterfaceHandler")))
            .AddMembers(
                MakeGetSetProperty(
                    NullableType(
                        IdentifierName("PathHandler")),
                    "PathHandler",
                    Token(SyntaxKind.PublicKeyword)),
                MakeGetOnlyProperty(
                    IdentifierName("DBusConnection"),
                    "Connection",
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AbstractKeyword)),
                MakeGetOnlyProperty(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword)),
                        "InterfaceName",
                        Token(SyntaxKind.PublicKeyword))
                    .WithInitializer(
                        EqualsValueClause(
                            MakeLiteralExpression(dBusInterface.Name!)))
                    .WithSemicolonToken(
                        Token(SyntaxKind.SemicolonToken)));

        AddHandlerProperties(ref cl, dBusInterface);
        AddHandlerIntrospect(ref cl, dBusInterface);
        AddHandlerMethods(ref cl, dBusInterface);
        AddHandlerSignals(ref cl, dBusInterface);

        return cl;
    }

    private void AddHandlerMethods(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        dBusInterface.Methods ??= [];

        var switchSections = List<SwitchSectionSyntax>();

        foreach (var dBusMethod in dBusInterface.Methods)
        {
            var inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
            var outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

            var switchSection = SwitchSection()
                .AddLabels(
                    CaseSwitchLabel(
                        MakeLiteralExpression(dBusMethod.Name!)));

            var switchSectionBlock = Block();

            var abstractMethodName = $"On{Pascalize(dBusMethod.Name.AsSpan())}Async";

            var abstractMethod = MethodDeclaration(
                    ParseValueTaskReturnType(outArgs), abstractMethodName)
                .WithParameterList(
                    ParameterList(
                        SingletonSeparatedList(
                            Parameter(
                                    Identifier("request"))
                                .WithType(
                                    IdentifierName("DBusMessage")))))
                .AddModifiers(
                    Token(SyntaxKind.ProtectedKeyword),
                    Token(SyntaxKind.AbstractKeyword))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken));

            if (inArgs?.Length > 0)
            {
                abstractMethod = abstractMethod.AddParameterListParameters(
                    ParseParameterList(inArgs));
            }

            cl = cl.AddMembers(abstractMethod);

            if (inArgs?.Length > 0)
            {
                for (var i = 0; i < inArgs.Length; i++)
                {
                    var identifier = inArgs[i].Name is not null
                        ? SanitizeIdentifier(Camelize(inArgs[i].Name.AsSpan()))
                        : $"arg{i}";

                    switchSectionBlock = switchSectionBlock.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(IdentifierName("var"))
                                .AddVariables(
                                    VariableDeclarator(identifier)
                                        .WithInitializer(
                                            EqualsValueClause(
                                                MakeBodyCastExpression(inArgs[i].DBusDotnetType, "request", i))))));
                }
            }

            var callArgs = ArgumentList(
                SingletonSeparatedList(
                    Argument(
                        IdentifierName("request"))));

            if (inArgs?.Length > 0)
            {
                callArgs = callArgs.AddArguments(
                    inArgs.Select((argument, i) =>
                            Argument(
                                IdentifierName(argument.Name is not null
                                    ? SanitizeIdentifier(Camelize(argument.Name.AsSpan()))
                                    : $"arg{i}")))
                        .ToArray());
            }

            ExpressionSyntax callAbstractMethod = AwaitExpression(
                InvocationExpression(
                        IdentifierName(abstractMethodName))
                    .WithArgumentList(callArgs));

            if (outArgs is null || outArgs.Length == 0)
            {
                switchSectionBlock = switchSectionBlock.AddStatements(
                    ExpressionStatement(callAbstractMethod),
                    ReturnStatement(
                        InvocationExpression(
                            MakeMemberAccessExpression("request", "CreateReply"))));
            }
            else
            {
                switchSectionBlock = switchSectionBlock.AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator("ret")
                                    .WithInitializer(
                                        EqualsValueClause(callAbstractMethod)))));

                if (outArgs.Length == 1)
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("request", "CreateReply"))
                                .AddArgumentListArguments(
                                    Argument(
                                        MakeToDbusValueExpression(
                                            outArgs[0].DBusDotnetType,
                                            IdentifierName("ret"))))));
                }
                else
                {
                    switchSectionBlock = switchSectionBlock.AddStatements(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("request", "CreateReply"))
                                .WithArgumentList(
                                    ArgumentList(
                                        SeparatedList(
                                            outArgs.Select((argument, index) => Argument(
                                                    MakeToDbusValueExpression(
                                                        argument.DBusDotnetType,
                                                        MakeMemberAccessExpression("ret", $"Item{index + 1}"))))
                                                .ToArray())))));
                }
            }

            switchSection = switchSection.WithStatements(
                SingletonList<StatementSyntax>(switchSectionBlock));
            switchSections = switchSections.Add(switchSection);
        }

        switchSections = switchSections.Add(
            SwitchSection()
                .AddLabels(
                    DefaultSwitchLabel())
                .AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("request", "CreateError"))
                            .AddArgumentListArguments(
                                Argument(
                                    MakeLiteralExpression("org.freedesktop.DBus.Error.UnknownMethod")),
                                Argument(
                                    MakeLiteralExpression("Unknown method"))))));

        cl = cl.AddMembers(
            MethodDeclaration(
                    GenericName("Task")
                        .AddTypeArgumentListArguments(
                            IdentifierName("DBusMessage")),
                    "HandleMethodAsync")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.AsyncKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("request"))
                        .WithType(
                            IdentifierName("DBusMessage")))
                .WithBody(
                    Block(
                        SwitchStatement(
                                MakeMemberAccessExpression("request", "Member"))
                            .WithSections(switchSections))));
    }

    private void AddHandlerProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        dBusInterface.Properties ??= [];

        foreach (var property in dBusInterface.Properties)
        {
            cl = cl.AddMembers(
                MakeGetSetProperty(
                    property.DBusDotnetType.ToTypeSyntax(),
                    Pascalize(property.Name.AsSpan()),
                    Token(SyntaxKind.PublicKeyword),
                    Token(SyntaxKind.VirtualKeyword)));
        }

        cl = cl.AddMembers(
            MakeTryGetPropertyMethod(dBusInterface),
            MakeTrySetPropertyMethod(dBusInterface),
            MakeGetAllPropertiesMethod(dBusInterface));
    }

    private MethodDeclarationSyntax MakeTryGetPropertyMethod(DBusInterface dBusInterface)
    {
        var readable = dBusInterface.Properties!.Where(static x => x.Access is null or "read" or "readwrite").ToArray();

        var sections = readable.Select(property =>
            SwitchSection()
                .AddLabels(CaseSwitchLabel(MakeLiteralExpression(property.Name!)))
                .AddStatements(
                    ExpressionStatement(
                        AssignmentExpression(
                            SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName("value"),
                            ObjectCreationExpression(IdentifierName("DBusVariant"))
                                .AddArgumentListArguments(
                                    Argument(
                                        MakeToDbusValueExpression(
                                            property.DBusDotnetType,
                                            IdentifierName(Pascalize(property.Name.AsSpan()))))))),
                    ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression))));

        var defaultSection = SwitchSection()
            .AddLabels(DefaultSwitchLabel())
            .AddStatements(
                ExpressionStatement(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("value"),
                        PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression,
                            LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));

        return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), "TryGetProperty")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("name"))
                    .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                Parameter(Identifier("value"))
                    .WithType(IdentifierName("DBusVariant"))
                    .AddModifiers(Token(SyntaxKind.OutKeyword)))
            .WithBody(
                Block(
                    SwitchStatement(IdentifierName("name"))
                        .WithSections(List(sections.Concat([defaultSection])))));
    }

    private MethodDeclarationSyntax MakeTrySetPropertyMethod(DBusInterface dBusInterface)
    {
        var writable = dBusInterface.Properties!.Where(static x => x.Access is null or "write" or "readwrite").ToArray();

        var sections = writable.Select(property =>
            SwitchSection()
                .AddLabels(CaseSwitchLabel(MakeLiteralExpression(property.Name!)))
                .AddStatements(
                    ExpressionStatement(
                        AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                            IdentifierName(Pascalize(property.Name.AsSpan())),
                            MakeFromDbusValueExpression(
                                property.DBusDotnetType,
                                MakeMemberAccessExpression("value", "Value")))),
                    ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression))));

        var defaultSection = SwitchSection()
            .AddLabels(DefaultSwitchLabel())
            .AddStatements(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));

        return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), "TrySetProperty")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(Identifier("name"))
                    .WithType(PredefinedType(Token(SyntaxKind.StringKeyword))),
                Parameter(Identifier("value"))
                    .WithType(IdentifierName("DBusVariant")))
            .WithBody(
                Block(
                    SwitchStatement(IdentifierName("name"))
                        .WithSections(List(sections.Concat([defaultSection])))));
    }

    private MethodDeclarationSyntax MakeGetAllPropertiesMethod(DBusInterface dBusInterface)
    {
        var readable = dBusInterface.Properties!.Where(static x => x.Access is null or "read" or "readwrite").ToArray();

        var addStatements = readable.Select(property =>
            ExpressionStatement(
                InvocationExpression(
                        MakeMemberAccessExpression("items", "Add"))
                    .AddArgumentListArguments(
                        Argument(
                            ObjectCreationExpression(
                                    GenericName("KeyValuePair")
                                        .AddTypeArgumentListArguments(
                                            PredefinedType(Token(SyntaxKind.StringKeyword)),
                                            IdentifierName("DBusVariant")))
                                .AddArgumentListArguments(
                                    Argument(MakeLiteralExpression(property.Name!)),
                                    Argument(
                                        ObjectCreationExpression(IdentifierName("DBusVariant"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    MakeToDbusValueExpression(
                                                        property.DBusDotnetType,
                                                        IdentifierName(Pascalize(property.Name.AsSpan())))))))))));

        return MethodDeclaration(
                GenericName("Dictionary")
                    .AddTypeArgumentListArguments(
                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                        IdentifierName("DBusVariant")),
                "GetAllProperties")
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithBody(
                Block(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator("items")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            ObjectCreationExpression(
                                                    GenericName("List")
                                                        .AddTypeArgumentListArguments(
                                                            GenericName("KeyValuePair")
                                                                .AddTypeArgumentListArguments(
                                                                    PredefinedType(Token(SyntaxKind.StringKeyword)),
                                                                    IdentifierName("DBusVariant"))))
                                                .WithArgumentList(ArgumentList()))))),
                    Block(addStatements),
                    ReturnStatement(
                        ObjectCreationExpression(
                                GenericName("Dictionary")
                                    .AddTypeArgumentListArguments(
                                        PredefinedType(Token(SyntaxKind.StringKeyword)),
                                        IdentifierName("DBusVariant")))
                            .AddArgumentListArguments(
                                Argument(IdentifierName("items"))))));
    }

    private static void AddHandlerIntrospect(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        XmlSerializer xmlSerializer = new(typeof(DBusInterface));
        using StringWriter stringWriter = new();
        using var xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true });
        xmlSerializer.Serialize(xmlWriter, dBusInterface);
        var introspect = stringWriter.ToString();

        cl = cl.AddMembers(
            MakeGetOnlyProperty(
                    PredefinedType(
                        Token(SyntaxKind.StringKeyword)),
                    "IntrospectXml",
                    Token(SyntaxKind.PublicKeyword))
                .WithInitializer(
                    EqualsValueClause(
                        MakeLiteralExpression(introspect)))
                .WithSemicolonToken(
                    Token(SyntaxKind.SemicolonToken)));
    }

    private void AddHandlerSignals(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Signals is null)
            return;

        foreach (var signal in dBusInterface.Signals)
        {
            var method = MethodDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.VoidKeyword)),
                    $"Emit{Pascalize(signal.Name.AsSpan())}")
                .AddModifiers(
                    Token(SyntaxKind.ProtectedKeyword));

            if (signal.Arguments?.Length > 0)
            {
                method = method.WithParameterList(
                    ParameterList(
                        SeparatedList(
                            signal.Arguments.Select(static (argument, i) => Parameter(
                                    Identifier(argument.Name is not null
                                        ? SanitizeIdentifier(
                                            Camelize(argument.Name.AsSpan()))
                                        : $"arg{i}"))
                                .WithType(
                                    argument.DBusDotnetType.ToTypeSyntax())))));
            }

            var body = Block();

            body = body.AddStatements(
                IfStatement(
                    BinaryExpression(SyntaxKind.EqualsExpression,
                        IdentifierName("PathHandler"),
                        LiteralExpression(SyntaxKind.NullLiteralExpression)),
                    ThrowStatement(
                        ObjectCreationExpression(IdentifierName("InvalidOperationException"))
                            .AddArgumentListArguments(
                                Argument(MakeLiteralExpression("Handler is not attached to a path."))))));

            var args = ArgumentList()
                .AddArguments(
                    Argument(
                        MakeMemberAccessExpression("PathHandler", "Path")),
                    Argument(
                        IdentifierName("InterfaceName")),
                    Argument(
                        MakeLiteralExpression(signal.Name!)));

            if (signal.Arguments?.Length > 0)
            {
                args = args.AddArguments(
                    signal.Arguments.Select((argument, i) =>
                            Argument(
                                MakeToDbusValueExpression(
                                    argument.DBusDotnetType,
                                    IdentifierName(argument.Name is not null
                                        ? SanitizeIdentifier(
                                            Camelize(argument.Name.AsSpan()))
                                        : $"arg{i}"))))
                        .ToArray());
            }

            body = body.AddStatements(
                LocalDeclarationStatement(
                    VariableDeclaration(IdentifierName("var"))
                        .AddVariables(
                            VariableDeclarator("message")
                                .WithInitializer(
                                    EqualsValueClause(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("DBusMessage", "CreateSignal"))
                                            .WithArgumentList(args))))));

            body = body.AddStatements(
                ExpressionStatement(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        IdentifierName("_"),
                        InvocationExpression(
                                MakeMemberAccessExpression("Connection", "Wire", "SendAsync"))
                            .AddArgumentListArguments(
                                Argument(
                                    IdentifierName("message"))))));

            cl = cl.AddMembers(method.WithBody(body));
        }
    }
}
