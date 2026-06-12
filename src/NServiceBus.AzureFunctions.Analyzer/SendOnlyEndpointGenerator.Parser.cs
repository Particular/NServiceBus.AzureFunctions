namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NServiceBus.Core.Analyzer;

public sealed partial class SendOnlyEndpointGenerator
{
    static class Parser
    {
        internal static SendOnlyEndpointSpecs Extract(GeneratorAttributeSyntaxContext context, SendOnlyEndpointDefinition sendOnlyEndpointDefinition, CancellationToken cancellationToken = default)
        {
            if (context.Attributes.Length == 0)
            {
                return SendOnlyEndpointSpecs.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!SendOnlyEndpointGeneratorKnownTypes.TryGet(context.SemanticModel.Compilation, out var knownTypes))
            {
                return SendOnlyEndpointSpecs.Empty;
            }

            return context.TargetSymbol switch
            {
                IMethodSymbol methodSymbol => ExtractFromMethod(methodSymbol, knownTypes, sendOnlyEndpointDefinition),
                _ => SendOnlyEndpointSpecs.Empty
            };
        }

        static SendOnlyEndpointSpecs ExtractFromMethod(IMethodSymbol methodSymbol, SendOnlyEndpointGeneratorKnownTypes knownTypes, SendOnlyEndpointDefinition sendOnlyEndpointDefinition)
        {
            var diagnostics = new List<Diagnostic>();
            var spec = ExtractSendOnlyEndpointSpec(methodSymbol, knownTypes, sendOnlyEndpointDefinition, diagnostics);
            var sendOnlyEndpoints = spec is null
                ? ImmutableEquatableArray<SendOnlyEndpointSpec>.Empty
                : ((SendOnlyEndpointSpec[])[spec]).ToImmutableEquatableArray();
            return new SendOnlyEndpointSpecs(sendOnlyEndpoints, diagnostics.ToImmutableEquatableArray());
        }

        static SendOnlyEndpointSpec? ExtractSendOnlyEndpointSpec(
            IMethodSymbol method,
            SendOnlyEndpointGeneratorKnownTypes knownTypes,
            SendOnlyEndpointDefinition sendOnlyEndpointDefinition,
            List<Diagnostic> diagnostics)
        {
            if (!method.TryGetAttribute(knownTypes.SendOnlyEndpointAttribute, out var sendOnlyEndpointAttribute)
                || sendOnlyEndpointAttribute.ConstructorArguments.Length == 0
                || sendOnlyEndpointAttribute.ConstructorArguments[0].Value is not string endpointName)
            {
                return null;
            }

            var problems = ImmutableList.CreateBuilder<string>();

            if (!method.IsStatic)
            {
                problems.Add("method must be static");
            }

            var normalizedEndpointName = KnownTypeNames.Normalize(endpointName);
            if (!KnownTypeNames.IsConfigureMethodFor(method.Name, normalizedEndpointName))
            {
                var expectedMethodName = KnownTypeNames.ConfigureMethodName(endpointName);
                problems.Add($"method name must follow the 'Configure{{EndpointName}}' convention matching '{expectedMethodName}'");
            }

            var resolution = ConfigureMethodResolver.Resolve(method, knownTypes.EndpointConfiguration, knownTypes.DelegateType);
            if (resolution.Problems.Count > 0)
            {
                problems.AddRange(resolution.Problems);
            }

            if (problems.Count > 0)
            {
                diagnostics.Add(method.CreateDiagnostic(DiagnosticIds.InvalidSendOnlyEndpointMethodDescriptor, method.Name, string.Join(", ", problems)));
                return null;
            }

            if (!resolution.IsSuccess)
            {
                return null;
            }

            var connectionSettingName = ExtractConnectionSettingName(sendOnlyEndpointAttribute);

            return new SendOnlyEndpointSpec(
                endpointName,
                connectionSettingName,
                sendOnlyEndpointDefinition.RegistrationMethodFullyQualified,
                resolution.Spec!.Value);
        }

        static string? ExtractConnectionSettingName(AttributeData attribute)
        {
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (namedArg is { Key: "Connection", Value.Value: string connectionName })
                {
                    return connectionName;
                }
            }

            return null;
        }
    }

    internal sealed record SendOnlyEndpointSpec(
        string EndpointName,
        string? ConnectionSettingName,
        string RegistrationMethodFullyQualified,
        ConfigureMethodSpec ConfigureMethod);

    internal readonly record struct SendOnlyEndpointSpecs(ImmutableEquatableArray<SendOnlyEndpointSpec> SendOnlyEndpoints, ImmutableEquatableArray<Diagnostic> Diagnostics) : IDiagnosticsSpec
    {
        public static SendOnlyEndpointSpecs Empty { get; } = new(ImmutableEquatableArray<SendOnlyEndpointSpec>.Empty, ImmutableEquatableArray<Diagnostic>.Empty);
    }

    internal readonly record struct SendOnlyEndpointDefinition
    {
        public string RegistrationMethodFullyQualified { get; } = $"global::{KnownTypeNames.AzureServiceBusFunctionsHostApplicationBuilderExtensions}.{KnownTypeNames.AddNServiceBusAzureServiceBusSendOnlyEndpoint}";

        public SendOnlyEndpointDefinition() { }
    }

    readonly struct SendOnlyEndpointGeneratorKnownTypes(
        INamedTypeSymbol sendOnlyEndpointAttribute,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol delegateType)
    {
        public INamedTypeSymbol SendOnlyEndpointAttribute { get; } = sendOnlyEndpointAttribute;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol DelegateType { get; } = delegateType;

        public static bool TryGet(Compilation compilation, out SendOnlyEndpointGeneratorKnownTypes knownTypes)
        {
            var sendOnlyEndpointAttribute = compilation.GetTypeByMetadataName(KnownTypeNames.NServiceBusSendOnlyFunctionAttribute);
            var endpointConfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType);
            var delegateType = compilation.GetTypeByMetadataName(KnownTypeNames.FunctionEndpointConfiguration);

            if (sendOnlyEndpointAttribute is null
                || endpointConfiguration is null
                || delegateType is null)
            {
                knownTypes = default;
                return false;
            }

            knownTypes = new SendOnlyEndpointGeneratorKnownTypes(
                sendOnlyEndpointAttribute,
                endpointConfiguration,
                delegateType);

            return true;
        }
    }
}