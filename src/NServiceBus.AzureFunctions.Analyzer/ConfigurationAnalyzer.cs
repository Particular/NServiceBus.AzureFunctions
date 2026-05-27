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
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpression
            || invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return;
        }

        AnalyzeEndpointConfiguration(context, invocationExpression, memberAccessExpression);
        AnalyzeSendAndReplyOptions(context, invocationExpression, memberAccessExpression);
    }

    static void AnalyzeEndpointConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        MemberAccessExpressionSyntax memberAccessExpression)
    {
        if (!NotAllowedEndpointConfigurationMethods.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var diagnosticDescriptor))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccessExpression, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!IsEndpointConfigurationReceiver(methodSymbol))
        {
            return;
        }

        if (!IsEndpointConfigurationAnalysisScope(invocationExpression, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, invocationExpression.GetLocation()));
    }

    static void AnalyzeSendAndReplyOptions(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        MemberAccessExpressionSyntax memberAccessExpression)
    {
        if (!NotAllowedSendAndReplyOptions.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var diagnosticDescriptor))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(memberAccessExpression, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        var receiverType = methodSymbol.ReceiverType?.ToDisplayString();
        if (receiverType is not ("NServiceBus.SendOptions" or "NServiceBus.ReplyOptions"))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(diagnosticDescriptor, invocationExpression.GetLocation()));
    }

    static bool IsEndpointConfigurationReceiver(IMethodSymbol methodSymbol)
    {
        var receiverType = methodSymbol.ReceiverType?.ToDisplayString();
        return receiverType == KnownTypeNames.EndpointConfigurationType
               || receiverType?.EndsWith($".extension({KnownTypeNames.EndpointConfigurationType})", StringComparison.Ordinal) == true;
    }

    static bool IsEndpointConfigurationAnalysisScope(
        InvocationExpressionSyntax invocationExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        for (SyntaxNode? current = invocationExpression; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax methodDeclaration
                && semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken) is { } methodSymbol
                && HasSupportedConfigureMethodSignature(methodSymbol))
            {
                return true;
            }

            if (current is LocalFunctionStatementSyntax localFunction
                && semanticModel.GetDeclaredSymbol(localFunction, cancellationToken) is { } localFunctionSymbol
                && HasSupportedConfigureMethodSignature(localFunctionSymbol))
            {
                return true;
            }

            if (current is AnonymousFunctionExpressionSyntax anonymousFunction
                && IsSendOnlyEndpointConfigurationCallback(anonymousFunction, semanticModel, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    static bool HasSupportedConfigureMethodSignature(IMethodSymbol method)
    {
        if (method.Parameters.Length == 0 || method.Parameters[0].Type.ToDisplayString() != KnownTypeNames.EndpointConfigurationType)
        {
            return false;
        }

        for (var i = 1; i < method.Parameters.Length; i++)
        {
            var parameterType = method.Parameters[i].Type.ToDisplayString();
            if (!AllowedConfigureMethodParameterTypes.Contains(parameterType))
            {
                return false;
            }
        }

        return true;
    }

    static bool IsSendOnlyEndpointConfigurationCallback(
        AnonymousFunctionExpressionSyntax anonymousFunction,
        SemanticModel semanticModel,
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
               && delegateType.TypeArguments[0].ToDisplayString() == KnownTypeNames.EndpointConfigurationType
               && (delegateType.TypeArguments.Length == 1
                   || delegateType.TypeArguments[1].ToDisplayString() == KnownTypeNames.IServiceCollection);
    }

    const string AddSendOnlyEndpointMethodName = "AddSendOnlyNServiceBusEndpoint";

    static readonly HashSet<string> AllowedConfigureMethodParameterTypes =
    [
        KnownTypeNames.EndpointConfigurationType,
        KnownTypeNames.IServiceCollection,
        KnownTypeNames.IConfiguration,
        KnownTypeNames.IHostEnvironment
    ];
}