namespace NServiceBus.AzureFunctions.CodeFixes;

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Analyzer;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class InvalidFunctionMethodCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => [DiagnosticIds.InvalidFunctionMethod];

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        var methodDeclaration = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        var classDeclaration = methodDeclaration.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (classDeclaration is null)
        {
            return;
        }

        var properties = diagnostic.Properties;
        var hasMissingAdditionalParameter = properties.Any(kvp => kvp.Key.StartsWith("Missing", StringComparison.Ordinal)
            && kvp.Key is not "MissingFunctionContext"
            && kvp.Key is not "MissingCancellationToken"
            && kvp.Key is not "MissingConfigureMethod");
        var hasFixes = properties.ContainsKey("MissingFunctionContext")
            || properties.ContainsKey("MissingCancellationToken")
            || hasMissingAdditionalParameter
            || properties.ContainsKey("MissingConfigureMethod");

        if (!hasFixes)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Fix NServiceBus function method",
                createChangedDocument: ct => FixFunctionMethod(context.Document, root, methodDeclaration, classDeclaration, properties, ct),
                equivalenceKey: DiagnosticIds.InvalidFunctionMethod),
            diagnostic);
    }

    static Task<Document> FixFunctionMethod(
        Document document,
        SyntaxNode root,
        MethodDeclarationSyntax methodDeclaration,
        ClassDeclarationSyntax classDeclaration,
        ImmutableDictionary<string, string?> properties,
        CancellationToken cancellationToken)
    {
        var newMethodDeclaration = methodDeclaration;
        var newClassDeclaration = classDeclaration;

        // Add missing parameters to the method signature
        var paramsToAdd = new List<ParameterSyntax>();

        foreach (var property in properties
                     .Where(kvp => kvp.Key.StartsWith("Missing", StringComparison.Ordinal)
                         && kvp.Key is not "MissingFunctionContext"
                         && kvp.Key is not "MissingCancellationToken"
                         && kvp.Key is not "MissingConfigureMethod"))
        {
            if (property.Value is null)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var propertySuffix = property.Key.Substring("Missing".Length);
            var parameterName = char.ToLowerInvariant(propertySuffix[0]) + propertySuffix.Substring(1);
            paramsToAdd.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName))
                .WithType(SyntaxFactory.ParseTypeName(property.Value)));
        }

        if (properties.ContainsKey("MissingFunctionContext"))
        {
            paramsToAdd.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier("context"))
                .WithType(SyntaxFactory.ParseTypeName("FunctionContext")));
        }

        if (properties.ContainsKey("MissingCancellationToken"))
        {
            paramsToAdd.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier("cancellationToken"))
                .WithType(SyntaxFactory.ParseTypeName("CancellationToken")));
        }

        if (paramsToAdd.Count > 0)
        {
            var existingParams = newMethodDeclaration.ParameterList.Parameters;
            var allParams = existingParams.AddRange(paramsToAdd);
            newMethodDeclaration = newMethodDeclaration.WithParameterList(
                newMethodDeclaration.ParameterList.WithParameters(allParams));
        }

        // Replace the method in the class
        newClassDeclaration = newClassDeclaration.ReplaceNode(methodDeclaration, newMethodDeclaration);

        // Add Configure method if missing
        if (properties.ContainsKey("MissingConfigureMethod")
            && properties.TryGetValue("ConfigureMethodName", out var configureMethodName)
            && configureMethodName is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configureMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier(configureMethodName))
                .WithModifiers(SyntaxFactory.TokenList(
                    SyntaxFactory.Token(SyntaxKind.StaticKeyword)))
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("endpointConfiguration"))
                            .WithType(SyntaxFactory.ParseTypeName("EndpointConfiguration")))))
                .WithBody(SyntaxFactory.Block())
                .NormalizeWhitespace();

            newClassDeclaration = newClassDeclaration.AddMembers(configureMethod);
        }

        var newRoot = root.ReplaceNode(classDeclaration, newClassDeclaration);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}