#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FunctionEndpointAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticIds.ClassMustBePartialDescriptor,
            DiagnosticIds.ShouldNotImplementIHandleMessagesDescriptor,
            DiagnosticIds.MethodMustBePartialDescriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (!FunctionEndpointAnalyzerKnownTypes.TryGet(context.Compilation, out var knownTypes))
        {
            return;
        }

        var type = (INamedTypeSymbol)context.Symbol;

        var hasAttribute = type.GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownTypes.NServiceBusFunctionAttribute));

        if (!hasAttribute)
        {
            return;
        }

        // NSBFUNC001: Class must be partial
        var declarations = type.DeclaringSyntaxReferences;
        var isPartial = declarations.Any(r =>
            r.GetSyntax(context.CancellationToken) is ClassDeclarationSyntax classDecl &&
            classDecl.Modifiers.Any(SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticIds.ClassMustBePartialDescriptor,
                type.Locations.FirstOrDefault(),
                type.Name));
        }

        // NSBFUNC002: Should not implement IHandleMessages<T>
        var implementsIHandleMessages = type.AllInterfaces
            .Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, knownTypes.IHandleMessages));

        if (implementsIHandleMessages)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticIds.ShouldNotImplementIHandleMessagesDescriptor,
                type.Locations.FirstOrDefault(),
                type.Name));
        }
    }

    static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        if (!FunctionEndpointAnalyzerKnownTypes.TryGet(context.Compilation, out var knownTypes))
        {
            return;
        }

        var method = (IMethodSymbol)context.Symbol;

        var hasAttribute = method.GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownTypes.NServiceBusFunctionAttribute));

        if (!hasAttribute)
        {
            return;
        }

        // NSBFUNC003: Method must be partial
        if (!method.IsPartialDefinition)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticIds.MethodMustBePartialDescriptor,
                method.Locations.FirstOrDefault(),
                method.Name));
        }

        // NSBFUNC001: Containing class must be partial (when attribute is on method)
        var containingType = method.ContainingType;
        var declarations = containingType.DeclaringSyntaxReferences;
        var isPartial = declarations.Any(r =>
            r.GetSyntax(context.CancellationToken) is ClassDeclarationSyntax classDecl &&
            classDecl.Modifiers.Any(SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticIds.ClassMustBePartialDescriptor,
                containingType.Locations.FirstOrDefault(),
                containingType.Name));
        }
    }
}