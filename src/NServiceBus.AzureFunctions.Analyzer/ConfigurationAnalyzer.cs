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
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var knownSymbols = new KnownSymbols(
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IServiceCollection),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IConfiguration),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.IHostEnvironment),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.SendOptions),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.ReplyOptions),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.AzureServiceBusServerlessTransport),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.ActionOfT),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.ActionOfT1T2),
            context.Compilation.GetTypeByMetadataName(KnownTypeNames.FunctionsHostApplicationBuilderExtensions));

        // EndpointConfiguration analysis in named methods: code-block-scoped so callbacks
        // are limited to qualifying methods only.
        context.RegisterCodeBlockStartAction<SyntaxKind>(blockStartContext =>
        {
            if (blockStartContext.OwningSymbol is IMethodSymbol method && HasSupportedConfigureMethodSignature(method, knownSymbols))
            {
                blockStartContext.RegisterSyntaxNodeAction(
                    nodeContext => AnalyzeEndpointConfiguration(nodeContext, EndpointConfigurationContext.AzureFunctionsEndpoint, knownSymbols),
                    SyntaxKind.InvocationExpression);
            }
        });

        // Send-only endpoint configuration: syntax-node-scoped because RegisterCodeBlockStartAction
        // does not expose the AnonymousFunctionExpressionSyntax parent chain needed to detect
        // the AddSendOnlyNServiceBusEndpoint callback.
        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeSendOnlyEndpointConfiguration(nodeContext, knownSymbols),
            SyntaxKind.InvocationExpression);

        // SendOptions/ReplyOptions: syntax-node-scoped, purely receiver-type checked.
        context.RegisterSyntaxNodeAction(
            nodeContext => AnalyzeSendAndReplyOptions(nodeContext, knownSymbols),
            SyntaxKind.InvocationExpression);
    }

    static void AnalyzeEndpointConfiguration(
        SyntaxNodeAnalysisContext context,
        EndpointConfigurationContext endpointContext,
        KnownSymbols knownSymbols)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpression } invocationExpression)
        {
            return;
        }

        if (memberAccessExpression.Name.Identifier.ValueText == UseTransportMethodName)
        {
            AnalyzeInvalidTransportConfiguration(context, invocationExpression, endpointContext, knownSymbols);
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

    static void AnalyzeSendOnlyEndpointConfiguration(SyntaxNodeAnalysisContext context, KnownSymbols knownSymbols)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpression } invocationExpression)
        {
            return;
        }

        if (!IsEndpointConfigurationReceiver(memberAccessExpression, context.SemanticModel, knownSymbols.EndpointConfiguration, context.CancellationToken))
        {
            return;
        }

        var endpointContext = GetSendOnlyEndpointContext(invocationExpression, context.SemanticModel, knownSymbols, context.CancellationToken);
        if (endpointContext is null)
        {
            return;
        }

        if (memberAccessExpression.Name.Identifier.ValueText == UseTransportMethodName)
        {
            AnalyzeInvalidTransportConfiguration(context, invocationExpression, endpointContext.Value, knownSymbols);
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
            GetEndpointContextLabel(endpointContext.Value),
            GetEndpointConfigurationReason(rule, endpointContext.Value)));
    }

    static void AnalyzeInvalidTransportConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        EndpointConfigurationContext endpointContext,
        KnownSymbols knownSymbols)
    {
        if (context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (UsesAllowedTransport(invocationExpression, methodSymbol, context.SemanticModel, knownSymbols, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticIds.InvalidEndpointTransportConfigurationDescriptor,
            invocationExpression.GetLocation(),
            "EndpointConfiguration.UseTransport",
            GetEndpointContextLabel(endpointContext),
            "Use AzureServiceBusServerlessTransport when calling EndpointConfiguration.UseTransport(...)."));
    }

    static EndpointConfigurationContext? GetSendOnlyEndpointContext(
        InvocationExpressionSyntax invocationExpression,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        for (SyntaxNode? current = invocationExpression; current is not null; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax anonymousFunction
                && IsSendOnlyEndpointConfigurationCallback(anonymousFunction, semanticModel, knownSymbols, cancellationToken))
            {
                return EndpointConfigurationContext.SendOnlyEndpoint;
            }
        }

        return null;
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

    static bool IsEndpointConfigurationReceiver(
        MemberAccessExpressionSyntax memberAccessExpression,
        SemanticModel semanticModel,
        INamedTypeSymbol? endpointConfigurationSymbol,
        CancellationToken cancellationToken)
    {
        var receiverType = semanticModel.GetTypeInfo(memberAccessExpression.Expression, cancellationToken).Type;
        return SymbolEqualityComparer.Default.Equals(receiverType, endpointConfigurationSymbol);
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
        if (anonymousFunction.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax invocationExpression } argumentList })
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

        // Restrict recognition to the NServiceBus-generated extension method on
        // NServiceBus.FunctionsHostApplicationBuilderExtensions. A name match alone would
        // catch any user-defined method that happens to share the name and arity.
        // C# 14 extension members nest inside a synthetic extension block type whose
        // ContainingType is the block (not the enclosing static class), so walk the
        // ContainingType chain to find the host class.
        if (methodSymbol.Name != KnownTypeNames.AddSendOnlyNServiceBusEndpoint
            || methodSymbol.Parameters.Length != 2
            || !ContainsType(methodSymbol, knownSymbols.FunctionsHostApplicationBuilderExtensions))
        {
            return false;
        }

        return methodSymbol.Parameters[1].Type is INamedTypeSymbol { TypeArguments.Length: 1 or 2 } delegateType
               && IsSupportedSendOnlyCallbackDelegate(delegateType, knownSymbols)
               && SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[0], knownSymbols.EndpointConfiguration)
               && (delegateType.TypeArguments.Length == 1
                   || SymbolEqualityComparer.Default.Equals(delegateType.TypeArguments[1], knownSymbols.IServiceCollection));
    }

    static bool IsSupportedSendOnlyCallbackDelegate(INamedTypeSymbol delegateType, KnownSymbols knownSymbols)
        => delegateType.TypeArguments.Length switch
        {
            1 => SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, knownSymbols.ActionOfT),
            2 => SymbolEqualityComparer.Default.Equals(delegateType.OriginalDefinition, knownSymbols.ActionOfT1T2),
            _ => false
        };

    static bool ContainsType(IMethodSymbol method, INamedTypeSymbol? target)
    {
        if (target is null)
        {
            return false;
        }

        for (INamedTypeSymbol? type = method.ContainingType; type is not null; type = type.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, target))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsAllowedConfigureMethodParameterType(ITypeSymbol parameterType, KnownSymbols knownSymbols)
        => SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IServiceCollection)
           || SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IConfiguration)
           || SymbolEqualityComparer.Default.Equals(parameterType, knownSymbols.IHostEnvironment);

    static string GetEndpointContextLabel(EndpointConfigurationContext endpointContext) => endpointContext == EndpointConfigurationContext.SendOnlyEndpoint ? SendOnlyEndpoints : AzureFunctionsEndpoints;

    static string GetEndpointConfigurationReason(
        InvalidEndpointConfigurationRule rule,
        EndpointConfigurationContext endpointContext)
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
    const string SendOnlyEndpointReason = "Send-only endpoints do not receive messages.";

    readonly record struct KnownSymbols(
        INamedTypeSymbol? EndpointConfiguration,
        INamedTypeSymbol? IServiceCollection,
        INamedTypeSymbol? IConfiguration,
        INamedTypeSymbol? IHostEnvironment,
        INamedTypeSymbol? SendOptions,
        INamedTypeSymbol? ReplyOptions,
        INamedTypeSymbol? AzureServiceBusServerlessTransport,
        INamedTypeSymbol? ActionOfT,
        INamedTypeSymbol? ActionOfT1T2,
        INamedTypeSymbol? FunctionsHostApplicationBuilderExtensions);

    enum EndpointConfigurationContext
    {
        AzureFunctionsEndpoint,
        SendOnlyEndpoint
    }

    readonly record struct InvalidEndpointConfigurationRule(string ApiName, string Reason, string? SendOnlyReason);

    readonly record struct InvalidSendOptionsRule(string Reason);

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

    static readonly Dictionary<string, InvalidSendOptionsRule> InvalidSendAndReplyOptions =
        new()
        {
            ["RouteReplyToThisInstance"] = new("Instances are ephemeral and cannot be directly addressed. Use endpoint routing instead."),
            ["RouteToThisInstance"] = new("Instances are ephemeral and cannot be directly addressed. Use endpoint routing instead.")
        };
}