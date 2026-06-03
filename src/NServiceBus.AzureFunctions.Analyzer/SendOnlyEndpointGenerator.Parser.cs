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

            var expectedMethodName = $"Configure{endpointName}";
            if (!string.Equals(method.Name, expectedMethodName, StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"method name must be '{expectedMethodName}'");
            }

            if (method.Parameters.Length == 0 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, knownTypes.EndpointConfiguration))
            {
                problems.Add("first parameter must be EndpointConfiguration");
            }

            for (var i = 1; i < method.Parameters.Length; i++)
            {
                if (!IsAllowedConfigureMethodParameterType(method.Parameters[i].Type, knownTypes))
                {
                    problems.Add("parameters after EndpointConfiguration must be IServiceCollection, IConfiguration, or IHostEnvironment");
                    break;
                }
            }

            if (problems.Count > 0)
            {
                diagnostics.Add(method.CreateDiagnostic(DiagnosticIds.InvalidSendOnlyEndpointMethodDescriptor, method.Name, string.Join(", ", problems)));
                return null;
            }

            var parameterTypeNames = new string[method.Parameters.Length];
            for (var i = 0; i < method.Parameters.Length; i++)
            {
                parameterTypeNames[i] = method.Parameters[i].Type.Name.ToLowerInvariant();
            }

            return new SendOnlyEndpointSpec(
                endpointName,
                sendOnlyEndpointDefinition.RegistrationMethodFullyQualified,
                new ConfigureMethodSpec(
                    method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    method.Name,
                    parameterTypeNames.ToImmutableEquatableArray()));
        }

        static bool IsAllowedConfigureMethodParameterType(ITypeSymbol parameterType, SendOnlyEndpointGeneratorKnownTypes knownTypes)
            => parameterType.IsAllowedConfigureMethodParameterType(knownTypes.IServiceCollection, knownTypes.IConfiguration, knownTypes.IHostEnvironment);
    }

    internal readonly record struct ConfigureMethodSpec(string ContainingTypeFullyQualified, string MethodName, ImmutableEquatableArray<string> ParameterTypeNames);

    internal sealed record SendOnlyEndpointSpec(
        string EndpointName,
        string RegistrationMethodFullyQualified,
        ConfigureMethodSpec ConfigureMethod);

    internal readonly record struct SendOnlyEndpointSpecs(ImmutableEquatableArray<SendOnlyEndpointSpec> SendOnlyEndpoints, ImmutableEquatableArray<Diagnostic> Diagnostics)
    {
        public static SendOnlyEndpointSpecs Empty { get; } = new(ImmutableEquatableArray<SendOnlyEndpointSpec>.Empty, ImmutableEquatableArray<Diagnostic>.Empty);
    }

    internal readonly record struct SendOnlyEndpointDefinition(string RegistrationMethodFullyQualified);

    readonly struct SendOnlyEndpointGeneratorKnownTypes(
        INamedTypeSymbol sendOnlyEndpointAttribute,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol iServiceCollection,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iHostEnvironment)
    {
        public INamedTypeSymbol SendOnlyEndpointAttribute { get; } = sendOnlyEndpointAttribute;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IServiceCollection { get; } = iServiceCollection;
        public INamedTypeSymbol IConfiguration { get; } = iConfiguration;
        public INamedTypeSymbol IHostEnvironment { get; } = iHostEnvironment;

        public static bool TryGet(Compilation compilation, out SendOnlyEndpointGeneratorKnownTypes knownTypes)
        {
            var sendOnlyEndpointAttribute = compilation.GetTypeByMetadataName(KnownTypeNames.NServiceBusSendOnlyEndpointAttribute);
            var endpointConfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType);
            var iServiceCollection = compilation.GetTypeByMetadataName(KnownTypeNames.IServiceCollection);
            var iconfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.IConfiguration);
            var iHostEnvironment = compilation.GetTypeByMetadataName(KnownTypeNames.IHostEnvironment);

            if (sendOnlyEndpointAttribute is null
                || endpointConfiguration is null
                || iServiceCollection is null
                || iconfiguration is null
                || iHostEnvironment is null)
            {
                knownTypes = default;
                return false;
            }

            knownTypes = new SendOnlyEndpointGeneratorKnownTypes(
                sendOnlyEndpointAttribute,
                endpointConfiguration,
                iServiceCollection,
                iconfiguration,
                iHostEnvironment);

            return true;
        }
    }
}