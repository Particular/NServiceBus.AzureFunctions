namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConfigurationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticIds.PurgeOnStartupNotAllowedDescriptor,
        DiagnosticIds.LimitMessageProcessingToNotAllowedDescriptor,
        DiagnosticIds.DefineCriticalErrorActionNotAllowedDescriptor,
        DiagnosticIds.SetDiagnosticsPathNotAllowedDescriptor,
        DiagnosticIds.MakeInstanceUniquelyAddressableNotAllowedDescriptor,
        DiagnosticIds.OverrideLocalAddressNotAllowedDescriptor,
        DiagnosticIds.RouteReplyToThisInstanceNotAllowedDescriptor,
        DiagnosticIds.RouteToThisInstanceNotAllowedDescriptor
    ];

    static readonly Dictionary<string, DiagnosticDescriptor> NotAllowedEndpointConfigurationMethods =
        new()
        {
            ["PurgeOnStartup"] = DiagnosticIds.PurgeOnStartupNotAllowedDescriptor,
            ["LimitMessageProcessingConcurrencyTo"] = DiagnosticIds.LimitMessageProcessingToNotAllowedDescriptor,
            ["DefineCriticalErrorAction"] = DiagnosticIds.DefineCriticalErrorActionNotAllowedDescriptor,
            ["SetDiagnosticsPath"] = DiagnosticIds.SetDiagnosticsPathNotAllowedDescriptor,
            ["MakeInstanceUniquelyAddressable"] = DiagnosticIds.MakeInstanceUniquelyAddressableNotAllowedDescriptor,
            ["UniquelyIdentifyRunningInstance"] = DiagnosticIds.MakeInstanceUniquelyAddressableNotAllowedDescriptor,
            ["OverrideLocalAddress"] = DiagnosticIds.OverrideLocalAddressNotAllowedDescriptor
        };

    static readonly Dictionary<string, DiagnosticDescriptor> NotAllowedSendAndReplyOptions =
        new()
        {
            ["RouteReplyToThisInstance"] = DiagnosticIds.RouteReplyToThisInstanceNotAllowedDescriptor,
            ["RouteToThisInstance"] = DiagnosticIds.RouteToThisInstanceNotAllowedDescriptor
        };

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var knownSymbols = new KnownSymbols(
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IServiceCollection),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IConfiguration),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IHostEnvironment),
            context.Compilation.GetTypeByMetadataName("NServiceBus.SendOptions"),
            context.Compilation.GetTypeByMetadataName("NServiceBus.ReplyOptions"));

        context.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, knownSymbols), SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context, KnownSymbols knownSymbols)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpression
            || invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return;
        }

        AnalyzeEndpointConfiguration(context, invocationExpression, memberAccessExpression, knownSymbols);
        AnalyzeSendAndReplyOptions(context, invocationExpression, memberAccessExpression, knownSymbols);
    }

    static void AnalyzeEndpointConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        MemberAccessExpressionSyntax memberAccessExpression,
        KnownSymbols knownSymbols)
    {
        if (!NotAllowedEndpointConfigurationMethods.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var diagnosticDescriptor))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccessExpression, context.CancellationToken).Symbol is not IMethodSymbol)
        {
            return;
        }

        if (!IsEndpointConfigurationReceiver(memberAccessExpression, context.SemanticModel, knownSymbols.EndpointConfiguration, context.CancellationToken))
        {
            return;
        }

        if (!IsEndpointConfigurationAnalysisScope(invocationExpression, context.SemanticModel, knownSymbols, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, invocationExpression.GetLocation()));
    }

    static void AnalyzeSendAndReplyOptions(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        MemberAccessExpressionSyntax memberAccessExpression,
        KnownSymbols knownSymbols)
    {
        if (!NotAllowedSendAndReplyOptions.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var diagnosticDescriptor))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccessExpression, context.CancellationToken).Symbol is not IMethodSymbol)
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccessExpression.Expression, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(receiverType, knownSymbols.SendOptions)
            && !SymbolEqualityComparer.Default.Equals(receiverType, knownSymbols.ReplyOptions))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, invocationExpression.GetLocation()));
    }

    static bool IsEndpointConfigurationReceiver(
        MemberAccessExpressionSyntax memberAccessExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol? endpointConfigurationSymbol,
        CancellationToken cancellationToken)
    {
        var receiverType = semanticModel.GetTypeInfo(memberAccessExpression.Expression, cancellationToken).Type;
        return SymbolEqualityComparer.Default.Equals(receiverType, endpointConfigurationSymbol);
    }

    static bool IsEndpointConfigurationAnalysisScope(
        InvocationExpressionSyntax invocationExpression,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        for (SyntaxNode? current = invocationExpression; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax methodDeclaration
                && semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is { } methodSymbol
                && HasSupportedConfigureMethodSignature(methodSymbol, knownSymbols))
            {
                return true;
            }

            if (current is LocalFunctionStatementSyntax localFunction
                && semanticModel.GetDeclaredSymbol(localFunction, cancellationToken) is { } localFunctionSymbol
                && HasSupportedConfigureMethodSignature(localFunctionSymbol, knownSymbols))
            {
                return true;
            }

            if (current is AnonymousFunctionExpressionSyntax anonymousFunction
                && IsSendOnlyEndpointConfigurationCallback(anonymousFunction, semanticModel, knownSymbols, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasSupportedConfigureMethodSignature(IMethodSymbol method, KnownSymbols knownSymbols)
    {
        if (method.Parameters.Length == 0
            || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, knownSymbols.EndpointConfiguration))
        {
            return false;
        }

        for (var i = 1; i < method.Parameters.Length; i++)
        {
            if (!IsAllowedConfigureMethodParameterType(method.Parameters[i].Type, knownSymbols))
            {
                return false;
            }
        }

        return true;
    }

    static bool IsSendOnlyEndpointConfigurationCallback(
        AnonymousFunctionExpressionSyntax anonymousFunction,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (anonymousFunction.Parent is not ArgumentSyntax argument
            || argument.Parent is not ArgumentListSyntax argumentList
            || argumentList.Parent is not InvocationExpressionSyntax invocationExpression)
        {
            return false;
        }

        if (argumentList.Arguments.Count == 0 || argumentList.Arguments[^1].Expression != anonymousFunction)
        {
            return false;
        }

        if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        if (methodSymbol.Name != AddSendOnlyEndpointMethodName || methodSymbol.Parameters.Length != 2)
        {
            return false;
        }

        return methodSymbol.Parameters[1].Type is INamedTypeSymbol delegateType
               && delegateType.Name == "Action"
               && delegateType.ContainingNamespace.ToDisplayString() == "System"
               && delegateType.TypeArguments.Length is 1 or 2
               && SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[0], knownSymbols.EndpointConfiguration)
               && (delegateType.TypeArguments.Length == 1
                   || SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[1], knownSymbols.IServiceCollection));
    }

    static bool IsAllowedConfigureMethodParameterType(ITypeSymbol parameterType, KnownSymbols knownSymbols)
        => SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IServiceCollection)
           || SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IConfiguration)
           || SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IHostEnvironment);

    const string AddSendOnlyEndpointMethodName = "AddSendOnlyNServiceBusEndpoint";

    readonly record struct KnownSymbols(
        INamedTypeSymbol? EndpointConfiguration,
        INamedTypeSymbol? IServiceCollection,
        INamedTypeSymbol? IConfiguration,
        INamedTypeSymbol? IHostEnvironment,
        INamedTypeSymbol? SendOptions,
        INamedTypeSymbol? ReplyOptions);
}