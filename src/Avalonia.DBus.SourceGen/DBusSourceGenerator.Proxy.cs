using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Avalonia.DBus.SourceGen;

public partial class DBusSourceGenerator
{
    private ClassDeclarationSyntax GenerateProxy(DBusInterface dBusInterface)
    {
        string identifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Proxy";
        ClassDeclarationSyntax cl = ClassDeclaration(identifier)
            .AddModifiers(Token(SyntaxKind.InternalKeyword));

        FieldDeclarationSyntax interfaceConst = MakePrivateStringConst("Interface", dBusInterface.Name!, PredefinedType(Token(SyntaxKind.StringKeyword)));
        FieldDeclarationSyntax connectionField = MakePrivateReadOnlyField("_connection", IdentifierName("DBusConnection"));
        FieldDeclarationSyntax destinationField = MakePrivateReadOnlyField("_destination", PredefinedType(Token(SyntaxKind.StringKeyword)));
        FieldDeclarationSyntax pathField = MakePrivateReadOnlyField("_path", IdentifierName("DBusObjectPath"));

        ConstructorDeclarationSyntax ctor = ConstructorDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("connection"))
                    .WithType(
                        IdentifierName("DBusConnection")),
                Parameter(
                        Identifier("destination"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))),
                Parameter(
                        Identifier("path"))
                    .WithType(
                        IdentifierName("DBusObjectPath")))
            .WithBody(
                Block(
                    MakeAssignmentExpressionStatement("_connection", "connection"),
                    MakeAssignmentExpressionStatement("_destination", "destination"),
                    MakeAssignmentExpressionStatement("_path", "path")));

        cl = cl.AddMembers(interfaceConst, connectionField, destinationField, pathField, ctor);

        AddProperties(ref cl, dBusInterface);
        AddProxyMethods(ref cl, dBusInterface);
        AddProxySignals(ref cl, dBusInterface);

        return cl;
    }

    private void AddProxyMethods(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Methods is null)
            return;

        foreach (DBusMethod dBusMethod in dBusInterface.Methods)
        {
            DBusArgument[]? inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
            DBusArgument[]? outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

            MethodDeclarationSyntax proxyMethod = MethodDeclaration(
                    ParseTaskReturnType(outArgs), $"{Pascalize(dBusMethod.Name.AsSpan())}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword));

            if (inArgs is not null)
            {
                proxyMethod = proxyMethod.WithParameterList(
                    ParameterList(
                        SeparatedList(
                            ParseParameterList(inArgs))));
            }

            var extraArgs = inArgs?.Select((arg, i) => (ExpressionSyntax)IdentifierName(arg.Name is not null
                    ? SanitizeIdentifier(Camelize(arg.Name.AsSpan()))
                    : $"arg{i}"))
                ?? [];

            InvocationExpressionSyntax call = InvocationExpression(
                    MakeMemberAccessExpression("_connection", "CallMethodAsync"))
                .WithArgumentList(
                    MakeCallArguments(IdentifierName("Interface"), dBusMethod.Name!, extraArgs));

            BlockSyntax body = Block();

            if (outArgs is null || outArgs.Length == 0)
            {
                body = body.AddStatements(ExpressionStatement(AwaitExpression(call)));
            }
            else
            {
                body = body.AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator("reply")
                                    .WithInitializer(
                                        EqualsValueClause(AwaitExpression(call))))));

                if (outArgs.Length == 1)
                {
                    body = body.AddStatements(
                        ReturnStatement(
                            MakeBodyCastExpression(outArgs[0].DBusDotnetType.ToTypeSyntax(), "reply", 0)));
                }
                else
                {
                    body = body.AddStatements(
                        ReturnStatement(
                            TupleExpression(
                                SeparatedList(
                                    outArgs.Select((argument, index) => Argument(
                                            MakeBodyCastExpression(argument.DBusDotnetType.ToTypeSyntax(), "reply", index)))
                                        .ToArray()))));
                }
            }

            cl = cl.AddMembers(proxyMethod.WithBody(body));
        }
    }

    private void AddProxySignals(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Signals is null)
            return;

        foreach (DBusSignal dBusSignal in dBusInterface.Signals)
        {
            DBusArgument[]? outArgs = dBusSignal.Arguments?.Where(static x => x.Direction is null or "out").ToArray();
            TypeSyntax? returnType = ParseReturnType(outArgs);

            ParameterListSyntax parameters = ParameterList();

            if (returnType is not null)
            {
                parameters = parameters.AddParameters(
                    Parameter(
                            Identifier("handler"))
                        .WithType(
                            GenericName("Action")
                                .AddTypeArgumentListArguments(
                                    outArgs!.Select(static argument => argument.DBusDotnetType.ToTypeSyntax())
                                        .ToArray())));
            }
            else
            {
                parameters = parameters.AddParameters(
                    Parameter(
                            Identifier("handler"))
                        .WithType(
                            IdentifierName("Action")));
            }

            parameters = parameters.AddParameters(
                Parameter(
                        Identifier("emitOnCapturedContext"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.BoolKeyword)))
                    .WithDefault(
                        EqualsValueClause(
                            LiteralExpression(SyntaxKind.TrueLiteralExpression))));

            MethodDeclarationSyntax watchSignalMethod = MethodDeclaration(
                    GenericName("Task")
                        .AddTypeArgumentListArguments(
                            IdentifierName("IDisposable")),
                    $"Watch{Pascalize(dBusSignal.Name.AsSpan())}Async")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .WithParameterList(parameters)
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("_connection", "SubscribeAsync"))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("_destination")),
                                    Argument(
                                        IdentifierName("_path")),
                                    Argument(
                                        IdentifierName("Interface")),
                                    Argument(
                                        MakeLiteralExpression(dBusSignal.Name!)),
                                    Argument(MakeSignalHandlerLambda(outArgs)),
                                    Argument(
                                        ConditionalExpression(
                                            IdentifierName("emitOnCapturedContext"),
                                            MakeMemberAccessExpression("SynchronizationContext", "Current"),
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))))));

            cl = cl.AddMembers(watchSignalMethod);
        }
    }

    private void AddWatchPropertiesChanged(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        cl = cl.AddMembers(
            MethodDeclaration(
                    GenericName("Task")
                        .AddTypeArgumentListArguments(
                            IdentifierName("IDisposable")),
                    "WatchPropertiesChangedAsync")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    Parameter(Identifier("handler"))
                        .WithType(
                            GenericName("Action")
                                .AddTypeArgumentListArguments(
                                    GenericName("PropertyChanges")
                                        .AddTypeArgumentListArguments(
                                            IdentifierName(
                                                GetPropertiesClassIdentifier(dBusInterface))))),
                    Parameter(
                            Identifier("emitOnCapturedContext"))
                        .WithType(
                            PredefinedType(
                                Token(SyntaxKind.BoolKeyword)))
                        .WithDefault(
                            EqualsValueClause(
                                LiteralExpression(SyntaxKind.TrueLiteralExpression))))
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("_connection", "SubscribeAsync"))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("_destination")),
                                    Argument(
                                        IdentifierName("_path")),
                                    Argument(
                                        MakeLiteralExpression("org.freedesktop.DBus.Properties")),
                                    Argument(
                                        MakeLiteralExpression("PropertiesChanged")),
                                    Argument(MakePropertiesChangedLambda(dBusInterface)),
                                    Argument(
                                        ConditionalExpression(
                                            IdentifierName("emitOnCapturedContext"),
                                            MakeMemberAccessExpression("SynchronizationContext", "Current"),
                                            LiteralExpression(SyntaxKind.NullLiteralExpression))))))));
    }

    private void AddProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Properties is null || dBusInterface.Properties.Length == 0)
            return;

        cl = dBusInterface.Properties.Aggregate(cl, (current, dBusProperty) => dBusProperty.Access switch
        {
            "read" => current.AddMembers(MakeGetMethod(dBusProperty)),
            "write" => current.AddMembers(MakeSetMethod(dBusProperty)),
            "readwrite" => current.AddMembers(MakeGetMethod(dBusProperty), MakeSetMethod(dBusProperty)),
            _ => current
        });

        AddGetAllMethod(ref cl, dBusInterface);
        AddReadProperties(ref cl, dBusInterface);
        AddPropertiesClass(ref cl, dBusInterface);
        AddWatchPropertiesChanged(ref cl, dBusInterface);
    }

    private MethodDeclarationSyntax MakeGetMethod(DBusProperty dBusProperty)
    {
        InvocationExpressionSyntax call = InvocationExpression(
                MakeMemberAccessExpression("_connection", "CallMethodAsync"))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "Get",
                    [
                        IdentifierName("Interface"),
                        MakeLiteralExpression(dBusProperty.Name!)
                    ]));

        BlockSyntax body = Block(
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(
                        VariableDeclarator("reply")
                            .WithInitializer(
                                EqualsValueClause(AwaitExpression(call))))),
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("DBusVariant"))
                    .AddVariables(
                        VariableDeclarator("variant")
                            .WithInitializer(
                                EqualsValueClause(
                                    CastExpression(
                                        IdentifierName("DBusVariant"),
                                        ElementAccessExpression(
                                                MakeMemberAccessExpression("reply", "Body"))
                                            .WithArgumentList(
                                                BracketedArgumentList(
                                                    SingletonSeparatedList(
                                                        Argument(
                                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))))))))),
            ReturnStatement(
                CastExpression(
                    dBusProperty.DBusDotnetType.ToTypeSyntax(),
                    MakeMemberAccessExpression("variant", "Value"))));

        return MethodDeclaration(
                ParseTaskReturnType([dBusProperty]), $"Get{Pascalize(dBusProperty.Name.AsSpan())}PropertyAsync")
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword))
            .WithBody(body);
    }

    private MethodDeclarationSyntax MakeSetMethod(DBusProperty dBusProperty)
    {
        InvocationExpressionSyntax call = InvocationExpression(
                MakeMemberAccessExpression("_connection", "CallMethodAsync"))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "Set",
                    [
                        IdentifierName("Interface"),
                        MakeLiteralExpression(dBusProperty.Name!),
                        ObjectCreationExpression(IdentifierName("DBusVariant"))
                            .AddArgumentListArguments(Argument(IdentifierName("value")))
                    ]));

        BlockSyntax body = Block(
            ExpressionStatement(AwaitExpression(call)));

        return MethodDeclaration(
                IdentifierName("Task"),
                $"Set{Pascalize(dBusProperty.Name.AsSpan())}PropertyAsync")
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword),
                Token(SyntaxKind.AsyncKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("value"))
                    .WithType(
                        dBusProperty.DBusDotnetType.ToTypeSyntax()))
            .WithBody(body);
    }

    private static void AddGetAllMethod(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        InvocationExpressionSyntax call = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName("CallMethodAsync")))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "GetAll",
                    [IdentifierName("Interface")]));

        BlockSyntax body = Block(
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(
                        VariableDeclarator("reply")
                            .WithInitializer(
                                EqualsValueClause(AwaitExpression(call))))),
            LocalDeclarationStatement(
                VariableDeclaration(IdentifierName("var"))
                    .AddVariables(
                        VariableDeclarator("dict")
                            .WithInitializer(
                                EqualsValueClause(
                                    CastExpression(
                                        GenericName("DBusDict")
                                            .AddTypeArgumentListArguments(
                                                PredefinedType(Token(SyntaxKind.StringKeyword)),
                                                IdentifierName("DBusVariant")),
                                        ElementAccessExpression(
                                                MakeMemberAccessExpression("reply", "Body"))
                                            .WithArgumentList(
                                                BracketedArgumentList(
                                                    SingletonSeparatedList(
                                                        Argument(
                                                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0))))))))))),
            ReturnStatement(
                InvocationExpression(IdentifierName("ReadProperties"))
                    .AddArgumentListArguments(
                        Argument(IdentifierName("dict")))));

        cl = cl.AddMembers(
            MethodDeclaration(
                    GenericName("Task")
                        .AddTypeArgumentListArguments(
                            IdentifierName(
                                GetPropertiesClassIdentifier(dBusInterface))),
                    "GetAllPropertiesAsync")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword))
                .WithBody(body));
    }

    private static void AddPropertiesClass(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        ClassDeclarationSyntax propertiesClass = ClassDeclaration(
                GetPropertiesClassIdentifier(dBusInterface))
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword));

        propertiesClass = dBusInterface.Properties!.Aggregate(propertiesClass, static (current, property) =>
            current.AddMembers(
                MakeGetSetProperty(
                    DBusDotnetType.FromDBusValue(property)
                        .ToTypeSyntax(),
                    Pascalize(property.Name.AsSpan()),
                    Token(SyntaxKind.PublicKeyword))));

        cl = cl.AddMembers(propertiesClass);
    }

    private void AddReadProperties(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        var switchSections = new System.Collections.Generic.List<SwitchSectionSyntax>();
        foreach (var property in dBusInterface.Properties!)
        {
            var statements = new System.Collections.Generic.List<StatementSyntax>
            {
                ExpressionStatement(
                    AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        MakeMemberAccessExpression("props", Pascalize(property.Name.AsSpan())),
                        CastExpression(
                            property.DBusDotnetType.ToTypeSyntax(),
                            MakeMemberAccessExpression("entry", "Value", "Value")))),
                ExpressionStatement(
                    ConditionalAccessExpression(
                        IdentifierName("changed"),
                        InvocationExpression(
                                MemberBindingExpression(
                                    IdentifierName("Add")))
                            .AddArgumentListArguments(
                                Argument(
                                    MakeLiteralExpression(
                                        Pascalize(property.Name.AsSpan())))))),
                BreakStatement()
            };

            switchSections.Add(
                SwitchSection()
                    .AddLabels(
                        CaseSwitchLabel(
                            MakeLiteralExpression(property.Name!)))
                    .AddStatements(statements.ToArray()));
        }

        StatementSyntax propsDeclaration = LocalDeclarationStatement(
            VariableDeclaration(
                    IdentifierName(
                        GetPropertiesClassIdentifier(dBusInterface)))
                .AddVariables(
                    VariableDeclarator("props")
                        .WithInitializer(
                            EqualsValueClause(
                                ObjectCreationExpression(
                                        IdentifierName(
                                            GetPropertiesClassIdentifier(dBusInterface)))
                                    .WithArgumentList(ArgumentList())))));

        StatementSyntax foreachStatement = ForEachStatement(
            IdentifierName("var"),
            Identifier("entry"),
            IdentifierName("values"),
            Block(
                SwitchStatement(
                        MakeMemberAccessExpression("entry", "Key"))
                    .WithSections(List(switchSections))));

        BlockSyntax body = Block(
            propsDeclaration,
            foreachStatement,
            ReturnStatement(
                IdentifierName("props")));

        cl = cl.AddMembers(
            MethodDeclaration(
                    IdentifierName(
                        GetPropertiesClassIdentifier(dBusInterface)),
                    "ReadProperties")
                .AddModifiers(
                    Token(SyntaxKind.PrivateKeyword),
                    Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(
                    Parameter(
                            Identifier("values"))
                        .WithType(
                            GenericName("DBusDict")
                                .AddTypeArgumentListArguments(
                                    PredefinedType(Token(SyntaxKind.StringKeyword)),
                                    IdentifierName("DBusVariant"))),
                    Parameter
                            (Identifier("changed"))
                        .WithType(
                            NullableType(
                                GenericName("List")
                                    .AddTypeArgumentListArguments(
                                        PredefinedType(
                                            Token(SyntaxKind.StringKeyword)))))
                        .WithDefault(
                            EqualsValueClause(
                                LiteralExpression(SyntaxKind.NullLiteralExpression))))
                .WithBody(body));
    }

    private static ArgumentListSyntax MakeCallArguments(ExpressionSyntax interfaceExpression, string methodName, IEnumerable<ExpressionSyntax>? extraArgs)
    {
        ArgumentListSyntax args = ArgumentList()
            .AddArguments(
                Argument(
                    IdentifierName("_destination")),
                Argument(
                    IdentifierName("_path")),
                Argument(interfaceExpression),
                Argument(
                    MakeLiteralExpression(methodName)));

        if (extraArgs is not null)
        {
            foreach (ExpressionSyntax arg in extraArgs)
            {
                args = args.AddArguments(Argument(arg));
            }
        }

        return args;
    }

    private static ExpressionSyntax MakeBodyCastExpression(TypeSyntax type, string messageIdentifier, int index)
    {
        return CastExpression(
            type,
            ElementAccessExpression(
                    MakeMemberAccessExpression(messageIdentifier, "Body"))
                .WithArgumentList(
                    BracketedArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(index)))))));
    }

    private static ParenthesizedLambdaExpressionSyntax MakeSignalHandlerLambda(DBusArgument[]? args)
    {
        ParameterSyntax parameter = Parameter(Identifier("message"))
            .WithType(IdentifierName("DBusMessage"));

        var statements = new SyntaxList<StatementSyntax>();

        if (args is not null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string argName = args[i].Name is not null
                    ? SanitizeIdentifier(Camelize(args[i].Name.AsSpan()))
                    : $"arg{i}";

                statements = statements.Add(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator(argName)
                                    .WithInitializer(
                                        EqualsValueClause(
                                            MakeBodyCastExpression(args[i].DBusDotnetType.ToTypeSyntax(), "message", i))))));
            }
        }

        InvocationExpressionSyntax invoke = InvocationExpression(
            IdentifierName("handler"));

        if (args is not null && args.Length > 0)
        {
            invoke = invoke.WithArgumentList(
                ArgumentList(
                    SeparatedList(
                        args.Select((arg, i) => Argument(
                                IdentifierName(arg.Name is not null
                                    ? SanitizeIdentifier(Camelize(arg.Name.AsSpan()))
                                    : $"arg{i}")))
                            .ToArray())));
        }

        statements = statements.Add(ExpressionStatement(invoke));
        statements = statements.Add(ReturnStatement(MakeMemberAccessExpression("Task", "CompletedTask")));

        return ParenthesizedLambdaExpression()
            .AddParameterListParameters(parameter)
            .WithBlock(Block(statements));
    }

    private ParenthesizedLambdaExpressionSyntax MakePropertiesChangedLambda(DBusInterface dBusInterface)
    {
        string propsType = GetPropertiesClassIdentifier(dBusInterface);

        var statements = new StatementSyntax[]
        {
            ParseStatement("if (!string.Equals((string)message.Body[0], Interface, StringComparison.Ordinal))\n{\n    return Task.CompletedTask;\n}\n"),
            ParseStatement("var changed = new List<string>();"),
            ParseStatement($"var props = ReadProperties((DBusDict<string, DBusVariant>)message.Body[1], changed);"),
            ParseStatement("var invalidated = (DBusArray<string>)message.Body[2];"),
            ParseStatement($"handler(new PropertyChanges<{propsType}>(props, invalidated.ToArray(), changed.ToArray()));"),
            ParseStatement("return Task.CompletedTask;")
        };

        return ParenthesizedLambdaExpression()
            .AddParameterListParameters(
                Parameter(Identifier("message"))
                    .WithType(IdentifierName("DBusMessage")))
            .WithBlock(Block(statements));
    }
}
