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
        DiagnosticIds.InvalidEndpointConfigurationDescriptor,
        DiagnosticIds.InvalidSendOptionsDescriptor,
        DiagnosticIds.InvalidEndpointTransportConfigurationDescriptor
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var knownSymbols = new KnownSymbols(
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType),
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.FunctionEndpointConfiguration),
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.SendOptions),
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.ReplyOptions),
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.AzureServiceBusServerlessTransport),
                compilationStartContext.Compilation.GetTypeByMetadataName(KnownTypeNames.NServiceBusSendOnlyFunctionAttribute));

            compilationStartContext.RegisterCodeBlockStartAction<SyntaxKind>(blockStartContext =>
            {
                if (blockStartContext.OwningSymbol is not IMethodSymbol method)
                {
                    return;
                }

                if (!HasSupportedConfigureMethodSignature(method, knownSymbols))
                {
                    return;
                }

                var endpointContext = HasSendOnlyEndpointAttribute(method, knownSymbols.SendOnlyEndpointAttribute) ? EndpointConfigurationContext.SendOnlyEndpoint : EndpointConfigurationContext.AzureFunctionsEndpoint;

                blockStartContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeEndpointConfigurationInvocation(nodeContext, endpointContext, knownSymbols), SyntaxKind.InvocationExpression);
            });

            compilationStartContext.RegisterSyntaxNodeAction(nodeContext => AnalyzeSendAndReplyOptions(nodeContext, knownSymbols), SyntaxKind.InvocationExpression);
        });
    }

    static void AnalyzeEndpointConfigurationInvocation(SyntaxNodeAnalysisContext context, EndpointConfigurationContext endpointContext, KnownSymbols knownSymbols)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpression } invocationExpression)
        {
            return;
        }

        if (memberAccessExpression.Name.Identifier.ValueText == UseTransportMethodName)
        {
            AnalyzeInvalidTransportConfiguration(invocationExpression, context.SemanticModel, context.ReportDiagnostic, endpointContext, knownSymbols, context.CancellationToken);
            return;
        }

        if (!InvalidEndpointConfigurationMethods.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var rule))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticIds.InvalidEndpointConfigurationDescriptor,
            invocationExpression.GetLocation(),
            rule.ApiName,
            GetEndpointContextLabel(endpointContext),
            GetEndpointConfigurationReason(rule, endpointContext)));
    }

    static void AnalyzeInvalidTransportConfiguration(
        InvocationExpressionSyntax invocationExpression,
        SemanticModel semanticModel,
        Action<Diagnostic> reportDiagnostic,
        EndpointConfigurationContext endpointContext,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (UsesAllowedTransport(invocationExpression, methodSymbol, semanticModel, knownSymbols, cancellationToken))
        {
            return;
        }

        reportDiagnostic(Diagnostic.Create(
            DiagnosticIds.InvalidEndpointTransportConfigurationDescriptor,
            invocationExpression.GetLocation(),
            "EndpointConfiguration.UseTransport",
            GetEndpointContextLabel(endpointContext)));
    }

    static void AnalyzeSendAndReplyOptions(SyntaxNodeAnalysisContext context, KnownSymbols knownSymbols)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpression } invocationExpression)
        {
            return;
        }

        if (!InvalidSendAndReplyOptions.TryGetValue(memberAccessExpression.Name.Identifier.ValueText, out var rule))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(memberAccessExpression.Expression, context.CancellationToken).Type;
        if (!SymbolEqualityComparer.Default.Equals(receiverType, knownSymbols.SendOptions)
            && !SymbolEqualityComparer.Default.Equals(receiverType, knownSymbols.ReplyOptions))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticIds.InvalidSendOptionsDescriptor,
            invocationExpression.GetLocation(),
            $"{receiverType!.Name}.{memberAccessExpression.Name.Identifier.ValueText}",
            AzureFunctionsEndpoints,
            rule.Reason));
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
            var delegateParameters = knownSymbols.DelegateType?.DelegateInvokeMethod?.Parameters;
            if (delegateParameters is null)
            {
                return false;
            }

            var matched = false;
            for (var j = 1; j < delegateParameters.Value.Length; j++)
            {
                if (method.Parameters[i].Type.IsAssignableToDelegateParameter((INamedTypeSymbol)delegateParameters.Value[j].Type))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }

    static bool HasSendOnlyEndpointAttribute(IMethodSymbol method, INamedTypeSymbol? sendOnlyEndpointAttribute)
        => sendOnlyEndpointAttribute is not null && method.HasAttribute(sendOnlyEndpointAttribute);

    static string GetEndpointContextLabel(EndpointConfigurationContext endpointContext) => endpointContext == EndpointConfigurationContext.SendOnlyEndpoint ? SendOnlyEndpoints : AzureFunctionsEndpoints;

    static string GetEndpointConfigurationReason(InvalidEndpointConfigurationRule rule, EndpointConfigurationContext endpointContext)
    {
        if (endpointContext == EndpointConfigurationContext.SendOnlyEndpoint
            && rule.SendOnlyReason is { } sendOnlyReason)
        {
            return sendOnlyReason;
        }

        return rule.Reason;
    }

    static bool UsesAllowedTransport(
        InvocationExpressionSyntax invocationExpression,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        if (methodSymbol is { IsGenericMethod: true, TypeArguments.Length: 1 }
            && SymbolEqualityComparer.Default.Equals(methodSymbol.TypeArguments[0], knownSymbols.AzureServiceBusServerlessTransport))
        {
            return true;
        }

        if (invocationExpression.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        var typeInfo = semanticModel.GetTypeInfo(invocationExpression.ArgumentList.Arguments[0].Expression, cancellationToken);
        return SymbolEqualityComparer.Default.Equals(typeInfo.Type, knownSymbols.AzureServiceBusServerlessTransport)
               || SymbolEqualityComparer.Default.Equals(typeInfo.ConvertedType, knownSymbols.AzureServiceBusServerlessTransport);
    }

    const string UseTransportMethodName = "UseTransport";
    const string AzureFunctionsEndpoints = "Azure Functions endpoints";
    const string SendOnlyEndpoints = "Send-only endpoints";

    enum EndpointConfigurationContext
    {
        AzureFunctionsEndpoint,
        SendOnlyEndpoint
    }

    readonly record struct InvalidEndpointConfigurationRule(string ApiName, string Reason, string? SendOnlyReason);

    readonly record struct InvalidSendOptionsRule(string Reason);

    readonly record struct KnownSymbols(
        INamedTypeSymbol? EndpointConfiguration,
        INamedTypeSymbol? DelegateType,
        INamedTypeSymbol? SendOptions,
        INamedTypeSymbol? ReplyOptions,
        INamedTypeSymbol? AzureServiceBusServerlessTransport,
        INamedTypeSymbol? SendOnlyEndpointAttribute);

    static readonly Dictionary<string, InvalidEndpointConfigurationRule> InvalidEndpointConfigurationMethods =
        new()
        {
            ["PurgeOnStartup"] = new("EndpointConfiguration.PurgeOnStartup", "ServiceBusTrigger bindings do not support purging messages.", SendOnlyEndpointReason),
            ["LimitMessageProcessingConcurrencyTo"] = new("EndpointConfiguration.LimitMessageProcessingConcurrencyTo", "Concurrency is controlled by the host configuration.", SendOnlyEndpointReason),
            ["DefineCriticalErrorAction"] = new("EndpointConfiguration.DefineCriticalErrorAction", "These endpoints do not control the application lifecycle and should not define critical error behavior.", null),
            ["SetDiagnosticsPath"] = new("EndpointConfiguration.SetDiagnosticsPath", "Local file-system diagnostics are not supported. Use CustomDiagnosticsWriter instead.", null),
            ["MakeInstanceUniquelyAddressable"] = new("EndpointConfiguration.MakeInstanceUniquelyAddressable", "Instances have unpredictable lifecycles and should not be uniquely addressable.", SendOnlyEndpointReason),
            ["UniquelyIdentifyRunningInstance"] = new("EndpointConfiguration.UniquelyIdentifyRunningInstance", "Instances have unpredictable lifecycles and should not be uniquely addressable.", SendOnlyEndpointReason),
            ["OverrideLocalAddress"] = new("EndpointConfiguration.OverrideLocalAddress", "The endpoint address is determined by the trigger configuration.", SendOnlyEndpointReason)
        };

    const string SendOnlyEndpointReason = "Send-only endpoints do not receive messages.";

    static readonly Dictionary<string, InvalidSendOptionsRule> InvalidSendAndReplyOptions =
        new()
        {
            ["RouteReplyToThisInstance"] = new("Instances are ephemeral and cannot be directly addressed. Use endpoint routing instead."),
            ["RouteToThisInstance"] = new("Instances are ephemeral and cannot be directly addressed. Use endpoint routing instead.")
        };
}