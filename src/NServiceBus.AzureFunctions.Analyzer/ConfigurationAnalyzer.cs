namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
        var functionAttribute = context.Compilation.GetTypeByMetadataName(KnownTypeNames.FunctionAttribute);
        var nServiceBusFunctionAttribute = context.Compilation.GetTypeByMetadataName(KnownTypeNames.NServiceBusFunctionAttribute);

        if (functionAttribute is null || nServiceBusFunctionAttribute is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            nodeContext => Analyze(nodeContext, functionAttribute, nServiceBusFunctionAttribute),
            SyntaxKind.InvocationExpression);
    }

    static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol nServiceBusFunctionAttribute)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpression
            || invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccessExpression)
        {
            return;
        }

        AnalyzeEndpointConfiguration(context, invocationExpression, memberAccessExpression, functionAttribute, nServiceBusFunctionAttribute);
        AnalyzeSendAndReplyOptions(context, invocationExpression, memberAccessExpression);
    }

    static void AnalyzeEndpointConfiguration(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression,
        MemberAccessExpressionSyntax memberAccessExpression,
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol nServiceBusFunctionAttribute)
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

        if (!IsFunctionEndpointConfigureMethod(context.ContainingSymbol as IMethodSymbol, functionAttribute, nServiceBusFunctionAttribute))
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

    static bool IsFunctionEndpointConfigureMethod(
        IMethodSymbol? containingMethod,
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol nServiceBusFunctionAttribute)
    {
        if (containingMethod is null
            || containingMethod.MethodKind != MethodKind.Ordinary
            || containingMethod.ContainingType is null
            || !containingMethod.Name.StartsWith(ConfigureMethodPrefix, StringComparison.Ordinal)
            || !HasSupportedConfigureMethodSignature(containingMethod))
        {
            return false;
        }

        var functionName = containingMethod.Name[ConfigureMethodPrefix.Length..];
        if (functionName.Length == 0)
        {
            return false;
        }

        return containingMethod.ContainingType
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Any(candidate => IsMatchingFunction(candidate, functionName, functionAttribute, nServiceBusFunctionAttribute));
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

    static bool IsMatchingFunction(
        IMethodSymbol candidate,
        string functionName,
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol nServiceBusFunctionAttribute)
    {
        if (!candidate.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, nServiceBusFunctionAttribute)))
        {
            return false;
        }

        var functionAttributeData = candidate.GetAttributes()
            .FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, functionAttribute));

        return functionAttributeData?.ConstructorArguments is [{ Value: string configuredFunctionName }]
               && configuredFunctionName == functionName;
    }

    const string ConfigureMethodPrefix = "Configure";

    static readonly HashSet<string> AllowedConfigureMethodParameterTypes =
    [
        KnownTypeNames.EndpointConfigurationType,
        KnownTypeNames.IServiceCollection,
        KnownTypeNames.IConfiguration,
        KnownTypeNames.IHostEnvironment
    ];
}