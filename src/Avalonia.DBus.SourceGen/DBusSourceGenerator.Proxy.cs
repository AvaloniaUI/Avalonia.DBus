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
    private ClassDeclarationSyntax GenerateProxy(DBusInterface dBusInterface)
    {
        var identifier = $"{Pascalize(dBusInterface.Name.AsSpan())}Proxy";
        var cl = ClassDeclaration(identifier)
            .AddModifiers(Token(SyntaxKind.PublicKeyword));

        var interfaceConst = MakePrivateStringConst("DefaultInterface", dBusInterface.Name!, PredefinedType(Token(SyntaxKind.StringKeyword)));
        var interfaceField = MakePrivateReadOnlyField("_interface", PredefinedType(Token(SyntaxKind.StringKeyword)));
        var connectionField = MakePrivateReadOnlyField("_connection", IdentifierName("IDBusConnection"));
        var destinationField = MakePrivateReadOnlyField("_destination", PredefinedType(Token(SyntaxKind.StringKeyword)));
        var pathField = MakePrivateReadOnlyField("_path", IdentifierName("DBusObjectPath"));

        var ctor = ConstructorDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("connection"))
                    .WithType(
                        IdentifierName("IDBusConnection")),
                Parameter(
                        Identifier("destination"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))),
                Parameter(
                        Identifier("path"))
                    .WithType(
                        IdentifierName("DBusObjectPath")))
            .WithInitializer(
                ConstructorInitializer(
                    SyntaxKind.ThisConstructorInitializer,
                    ArgumentList(
                        SeparatedList(
                        [
                            Argument(IdentifierName("connection")),
                                Argument(IdentifierName("destination")),
                                Argument(IdentifierName("path")),
                                Argument(IdentifierName("DefaultInterface"))
                        ]))))
            .WithBody(
                Block());

        var ctorWithInterface = ConstructorDeclaration(identifier)
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                Parameter(
                        Identifier("connection"))
                    .WithType(
                        IdentifierName("IDBusConnection")),
                Parameter(
                        Identifier("destination"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))),
                Parameter(
                        Identifier("path"))
                    .WithType(
                        IdentifierName("DBusObjectPath")),
                Parameter(
                        Identifier("iface"))
                    .WithType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))))
            .WithBody(
                Block(
                    MakeAssignmentExpressionStatement("_connection", "connection"),
                    MakeAssignmentExpressionStatement("_destination", "destination"),
                    MakeAssignmentExpressionStatement("_path", "path"),
                    MakeAssignmentExpressionStatement("_interface", "iface")));

        cl = cl.AddMembers(interfaceConst, interfaceField, connectionField, destinationField, pathField, ctor, ctorWithInterface);

        AddProperties(ref cl, dBusInterface);
        AddProxyMethods(ref cl, dBusInterface);
        AddProxySignals(ref cl, dBusInterface);

        return cl;
    }

    private void AddProxyMethods(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        if (dBusInterface.Methods is null)
            return;

        foreach (var dBusMethod in dBusInterface.Methods)
        {
            var inArgs = dBusMethod.Arguments?.Where(static m => m.Direction is null or "in").ToArray();
            var outArgs = dBusMethod.Arguments?.Where(static m => m.Direction == "out").ToArray();

            var proxyMethod = MethodDeclaration(
                    ParseTaskReturnType(outArgs), $"{Pascalize(dBusMethod.Name.AsSpan())}Async")
                .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword));

            if (inArgs is not null)
            {
                proxyMethod = proxyMethod.WithParameterList(
                    ParameterList(
                        SeparatedList(
                            ParseParameterList(inArgs))));
            }

            var extraArgs = inArgs?.Select((arg, i) => MakeToDbusValueExpression(
                    arg.DBusDotnetType,
                    IdentifierName(arg.Name is not null
                        ? SanitizeIdentifier(Camelize(arg.Name.AsSpan()))
                        : $"arg{i}")))
                ?? [];

            var call = InvocationExpression(
                    MakeMemberAccessExpression("_connection", "CallMethodAsync"))
                .WithArgumentList(
                    MakeCallArguments(IdentifierName("_interface"), dBusMethod.Name!, extraArgs));

            var body = Block();

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
                            MakeBodyCastExpression(outArgs[0].DBusDotnetType, "reply", 0)));
                }
                else
                {
                    body = body.AddStatements(
                        ReturnStatement(
                            TupleExpression(
                                SeparatedList(
                                    outArgs.Select((argument, index) => Argument(
                                            MakeBodyCastExpression(argument.DBusDotnetType, "reply", index)))
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

        foreach (var dBusSignal in dBusInterface.Signals)
        {
            var outArgs = dBusSignal.Arguments?.Where(static x => x.Direction is null or "out").ToArray();
            var returnType = ParseReturnType(outArgs);

            ParameterSyntax handlerParameter;
            if (returnType is not null)
            {
                handlerParameter = Parameter(
                        Identifier("handler"))
                    .WithType(
                        GenericName("Action")
                            .AddTypeArgumentListArguments(
                                outArgs!.Select(static argument => argument.DBusDotnetType.ToTypeSyntax())
                                    .ToArray()));
            }
            else
            {
                handlerParameter = Parameter(
                        Identifier("handler"))
                    .WithType(
                        IdentifierName("Action"));
            }

            var emitOnCapturedContextParameter = Parameter(
                    Identifier("emitOnCapturedContext"))
                .WithType(
                    PredefinedType(
                        Token(SyntaxKind.BoolKeyword)))
                .WithDefault(
                    EqualsValueClause(
                        LiteralExpression(SyntaxKind.TrueLiteralExpression)));

            var senderParameter = Parameter(
                    Identifier("sender"))
                .WithType(
                    NullableType(
                        PredefinedType(
                            Token(SyntaxKind.StringKeyword))));

            var parameters = ParameterList()
                .AddParameters(handlerParameter, emitOnCapturedContextParameter);

            var parametersWithSender = ParameterList()
                .AddParameters(handlerParameter, senderParameter, emitOnCapturedContextParameter);

            var watchSignalMethodWithSender = MethodDeclaration(
                    GenericName("Task")
                        .AddTypeArgumentListArguments(
                            IdentifierName("IDisposable")),
                    $"Watch{Pascalize(dBusSignal.Name.AsSpan())}Async")
                .AddModifiers(
                    Token(SyntaxKind.PublicKeyword))
                .WithParameterList(parametersWithSender)
                .WithBody(
                    Block(
                        ReturnStatement(
                            InvocationExpression(
                                    MakeMemberAccessExpression("_connection", "SubscribeAsync"))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("sender")),
                                    Argument(
                                        IdentifierName("_path")),
                                    Argument(
                                        IdentifierName("_interface")),
                                    Argument(
                                        MakeLiteralExpression(dBusSignal.Name!)),
                                    Argument(MakeSignalHandlerLambda(outArgs)),
                                    Argument(
                                        ConditionalExpression(
                                            IdentifierName("emitOnCapturedContext"),
                                            MakeMemberAccessExpression("SynchronizationContext", "Current"),
                                            LiteralExpression(SyntaxKind.NullLiteralExpression)))))));

            var watchSignalMethod = MethodDeclaration(
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
                                    IdentifierName($"Watch{Pascalize(dBusSignal.Name.AsSpan())}Async"))
                                .AddArgumentListArguments(
                                    Argument(
                                        IdentifierName("handler")),
                                    Argument(
                                        IdentifierName("_destination")),
                                    Argument(
                                        IdentifierName("emitOnCapturedContext"))))));

            cl = cl.AddMembers(watchSignalMethodWithSender, watchSignalMethod);
        }
    }

    private void AddWatchPropertiesChanged(ref ClassDeclarationSyntax cl, DBusInterface dBusInterface)
    {
        var handlerParameter = Parameter(Identifier("handler"))
            .WithType(
                GenericName("Action")
                    .AddTypeArgumentListArguments(
                        IdentifierName(GetPropertiesClassIdentifier(dBusInterface)),
                        ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword)))
                            .AddRankSpecifiers(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression()))),
                        ArrayType(PredefinedType(Token(SyntaxKind.StringKeyword)))
                            .AddRankSpecifiers(ArrayRankSpecifier(SingletonSeparatedList<ExpressionSyntax>(OmittedArraySizeExpression())))));

        var emitOnCapturedContextParameter = Parameter(
                Identifier("emitOnCapturedContext"))
            .WithType(
                PredefinedType(
                    Token(SyntaxKind.BoolKeyword)))
            .WithDefault(
                EqualsValueClause(
                    LiteralExpression(SyntaxKind.TrueLiteralExpression)));

        var senderParameter = Parameter(
                Identifier("sender"))
            .WithType(
                NullableType(
                    PredefinedType(
                        Token(SyntaxKind.StringKeyword))));

        var watchPropertiesChangedWithSender = MethodDeclaration(
                GenericName("Task")
                    .AddTypeArgumentListArguments(
                        IdentifierName("IDisposable")),
                "WatchPropertiesChangedAsync")
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                handlerParameter,
                senderParameter,
                emitOnCapturedContextParameter)
            .WithBody(
                Block(
                    ReturnStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("_connection", "SubscribeAsync"))
                            .AddArgumentListArguments(
                                Argument(
                                    IdentifierName("sender")),
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
                                        LiteralExpression(SyntaxKind.NullLiteralExpression)))))));

        var watchPropertiesChanged = MethodDeclaration(
                GenericName("Task")
                    .AddTypeArgumentListArguments(
                        IdentifierName("IDisposable")),
                "WatchPropertiesChangedAsync")
            .AddModifiers(
                Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                handlerParameter,
                emitOnCapturedContextParameter)
            .WithBody(
                Block(
                    ReturnStatement(
                        InvocationExpression(
                                IdentifierName("WatchPropertiesChangedAsync"))
                            .AddArgumentListArguments(
                                Argument(
                                    IdentifierName("handler")),
                                Argument(
                                    IdentifierName("_destination")),
                                Argument(
                                    IdentifierName("emitOnCapturedContext"))))));

        cl = cl.AddMembers(watchPropertiesChangedWithSender, watchPropertiesChanged);
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
        var call = InvocationExpression(
                MakeMemberAccessExpression("_connection", "CallMethodAsync"))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "Get",
                    [
                        IdentifierName("_interface"),
                        MakeLiteralExpression(dBusProperty.Name!)
                    ]));

        var body = Block(
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
                MakeFromDbusValueExpression(
                    dBusProperty.DBusDotnetType,
                    MakeMemberAccessExpression("variant", "Value"))));

        return MethodDeclaration(
                ParseTaskReturnType([dBusProperty]), $"Get{Pascalize(dBusProperty.Name.AsSpan())}PropertyAsync")
            .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.AsyncKeyword))
            .WithBody(body);
    }

    private MethodDeclarationSyntax MakeSetMethod(DBusProperty dBusProperty)
    {
        var call = InvocationExpression(
                MakeMemberAccessExpression("_connection", "CallMethodAsync"))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "Set",
                    [
                        IdentifierName("_interface"),
                        MakeLiteralExpression(dBusProperty.Name!),
                        ObjectCreationExpression(IdentifierName("DBusVariant"))
                            .AddArgumentListArguments(
                                Argument(
                                    MakeToDbusValueExpression(
                                        dBusProperty.DBusDotnetType,
                                        IdentifierName("value"))))
                    ]));

        var body = Block(
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
        var call = InvocationExpression(
                MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName("_connection"), IdentifierName("CallMethodAsync")))
            .WithArgumentList(
                MakeCallArguments(
                    MakeLiteralExpression("org.freedesktop.DBus.Properties"),
                    "GetAll",
                    [IdentifierName("_interface")]));

        var body = Block(
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
                                        GenericName("Dictionary")
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
        var propertiesClass = ClassDeclaration(
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
        var switchSections = (from property in dBusInterface.Properties!
                              let statements = new List<StatementSyntax>
            {
                ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, MakeMemberAccessExpression("props", Pascalize(property.Name.AsSpan())), MakeFromDbusValueExpression(property.DBusDotnetType, MakeMemberAccessExpression("entry", "Value", "Value")))),
                ExpressionStatement(ConditionalAccessExpression(IdentifierName("changed"), InvocationExpression(MemberBindingExpression(IdentifierName("Add")))
                    .AddArgumentListArguments(Argument(MakeLiteralExpression(Pascalize(property.Name.AsSpan())))))),
                BreakStatement()
            }
                              select SwitchSection()
                                  .AddLabels(CaseSwitchLabel(MakeLiteralExpression(property.Name!)))
                                  .AddStatements(statements.ToArray())).ToList();

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

        var body = Block(
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
                            GenericName("Dictionary")
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
        var args = ArgumentList()
            .AddArguments(
                Argument(
                    IdentifierName("_destination")),
                Argument(
                    IdentifierName("_path")),
                Argument(interfaceExpression),
                Argument(
                    MakeLiteralExpression(methodName)),
                Argument(
                    MakeMemberAccessExpression("CancellationToken", "None")));

        if (extraArgs is not null)
        {
            foreach (var arg in extraArgs)
            {
                args = args.AddArguments(Argument(arg));
            }
        }

        return args;
    }

    private static ExpressionSyntax MakeBodyCastExpression(DBusDotnetType type, string messageIdentifier, int index)
    {
        ExpressionSyntax bodyAccess = ElementAccessExpression(
                MakeMemberAccessExpression(messageIdentifier, "Body"))
            .WithArgumentList(
                BracketedArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(index))))));

        return MakeFromDbusValueExpression(type, bodyAccess);
    }

    private static ParenthesizedLambdaExpressionSyntax MakeSignalHandlerLambda(DBusArgument[]? args)
    {
        var parameter = Parameter(Identifier("message"))
            .WithType(IdentifierName("DBusMessage"));

        var statements = new SyntaxList<StatementSyntax>();

        if (args is not null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                var argName = args[i].Name is not null
                    ? SanitizeIdentifier(Camelize(args[i].Name.AsSpan()))
                    : $"arg{i}";

                statements = statements.Add(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("var"))
                            .AddVariables(
                                VariableDeclarator(argName)
                                    .WithInitializer(
                                        EqualsValueClause(
                                            MakeBodyCastExpression(args[i].DBusDotnetType, "message", i))))));
            }
        }

        var invoke = InvocationExpression(
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
        var propsType = GetPropertiesClassIdentifier(dBusInterface);

        var statements = new[]
        {
            ParseStatement("if (!string.Equals((string)message.Body[0], _interface, StringComparison.Ordinal))\n{\n    return Task.CompletedTask;\n}\n"),
            ParseStatement("var changed = new List<string>();"),
            ParseStatement("var props = ReadProperties((Dictionary<string, DBusVariant>)message.Body[1], changed);"),
            ParseStatement("var invalidated = (List<string>)message.Body[2];"),
            ParseStatement("handler(props, invalidated.ToArray(), changed.ToArray());"),
            ParseStatement("return Task.CompletedTask;")
        };

        return ParenthesizedLambdaExpression()
            .AddParameterListParameters(
                Parameter(Identifier("message"))
                    .WithType(IdentifierName("DBusMessage")))
            .WithBlock(Block(statements));
    }

    private static string BuildGeneratedPrivateImplementationSource(
        IEnumerable<ProxyRegistration> proxyRegistrations,
        IEnumerable<HandlerRegistration> handlerRegistrations)
    {
        var proxyRegistrationArray = proxyRegistrations
            .OrderBy(static registration => registration.ProxyTypeName, StringComparer.Ordinal)
            .ToArray();
        var handlerRegistrationArray = handlerRegistrations
            .OrderBy(static registration => registration.HandlerMetadataTypeName, StringComparer.Ordinal)
            .ToArray();
        if (proxyRegistrationArray.Length == 0 && handlerRegistrationArray.Length == 0)
            return string.Empty;

        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Avalonia.DBus;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine($"namespace {PrivateImplementationNamespace}");
        sb.AppendLine("{");
        sb.AppendLine("    internal static class GeneratedPrivateImplementationDoNotTouch");
        sb.AppendLine("    {");
        sb.AppendLine("        [ModuleInitializer]");
        sb.AppendLine("        public static void Register()");
        sb.AppendLine("        {");
        foreach (var registration in proxyRegistrationArray)
        {
            var interfaceNameLiteral = SymbolDisplay.FormatLiteral(registration.InterfaceName, quote: true);
            sb.AppendLine("            DBusInteropMetadataRegistry.Register(");
            sb.AppendLine("                new DBusInteropMetadata");
            sb.AppendLine("                {");
            sb.AppendLine($"                    ClrType = typeof({registration.ProxyTypeName}),");
            sb.AppendLine($"                    InterfaceName = {interfaceNameLiteral},");
            sb.AppendLine($"                    CreateProxy = static (connection, destination, path, iface) => new {registration.ProxyTypeName}(connection, destination, path, iface)");
            sb.AppendLine("                });");
        }
        foreach (var registration in handlerRegistrationArray)
        {
            sb.AppendLine("            DBusInteropMetadataRegistry.Register(");
            sb.AppendLine("                new DBusInteropMetadata");
            sb.AppendLine("                {");
            sb.AppendLine($"                    ClrType = typeof({registration.HandlerInterfaceTypeName}),");
            sb.AppendLine($"                    InterfaceName = {registration.HandlerMetadataTypeName}.InterfaceName,");
            sb.AppendLine($"                    CreateCallDispatcher = static _ => {registration.HandlerMetadataTypeName}.Dispatcher,");
            sb.AppendLine($"                    TrySetProperty = {registration.HandlerMetadataTypeName}.TrySetProperty,");
            sb.AppendLine($"                    GetAllPropertiesFactory = {registration.HandlerMetadataTypeName}.GetAllProperties");
            sb.AppendLine("                });");
        }
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private readonly struct ProxyRegistration(string proxyTypeName, string interfaceName)
    {
        public string ProxyTypeName { get; } = proxyTypeName;

        public string InterfaceName { get; } = interfaceName;
    }

    private readonly struct HandlerRegistration(
        string handlerInterfaceTypeName,
        string handlerMetadataTypeName)
    {
        public string HandlerInterfaceTypeName { get; } = handlerInterfaceTypeName;

        public string HandlerMetadataTypeName { get; } = handlerMetadataTypeName;
    }
}
