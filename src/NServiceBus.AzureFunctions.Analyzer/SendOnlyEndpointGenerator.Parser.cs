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
                if (!method.Parameters[i].Type.IsAllowedConfigureMethodParameterType(
                        knownTypes.IServiceCollection, knownTypes.IConfigurationManager, knownTypes.IConfiguration, knownTypes.IConfigurationBuilder, knownTypes.IHostEnvironment))
                {
                    problems.Add($"parameters after EndpointConfiguration must be {TypeSymbolExtensions.AllowedConfigureParameterTypesDescription}");
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
                var type = method.Parameters[i].Type;
                // Map IConfiguration and IConfigurationBuilder to the delegate's IConfigurationManager argument name
                if (SymbolEqualityComparer.Default.Equals(type, knownTypes.IConfiguration)
                    || SymbolEqualityComparer.Default.Equals(type, knownTypes.IConfigurationBuilder))
                {
                    parameterTypeNames[i] = "iconfigurationmanager";
                }
                else
                {
                    parameterTypeNames[i] = type.Name.ToLowerInvariant();
                }
            }

            var connectionSettingName = ExtractConnectionSettingName(sendOnlyEndpointAttribute);

            return new SendOnlyEndpointSpec(
                endpointName,
                connectionSettingName,
                sendOnlyEndpointDefinition.RegistrationMethodFullyQualified,
                new ConfigureMethodSpec(
                    method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    method.Name,
                    parameterTypeNames.ToImmutableEquatableArray()));
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

    internal readonly record struct ConfigureMethodSpec(string ContainingTypeFullyQualified, string MethodName, ImmutableEquatableArray<string> ParameterTypeNames);

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
        INamedTypeSymbol iServiceCollection,
        INamedTypeSymbol iConfigurationManager,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iConfigurationBuilder,
        INamedTypeSymbol iHostEnvironment)
    {
        public INamedTypeSymbol SendOnlyEndpointAttribute { get; } = sendOnlyEndpointAttribute;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IServiceCollection { get; } = iServiceCollection;
        public INamedTypeSymbol IConfigurationManager { get; } = iConfigurationManager;
        public INamedTypeSymbol IConfiguration { get; } = iConfiguration;
        public INamedTypeSymbol IConfigurationBuilder { get; } = iConfigurationBuilder;
        public INamedTypeSymbol IHostEnvironment { get; } = iHostEnvironment;

        public static bool TryGet(Compilation compilation, out SendOnlyEndpointGeneratorKnownTypes knownTypes)
        {
            var sendOnlyEndpointAttribute = compilation.GetTypeByMetadataName(KnownTypeNames.NServiceBusSendOnlyFunctionAttribute);
            var endpointConfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType);
            var iServiceCollection = compilation.GetTypeByMetadataName(KnownTypeNames.IServiceCollection);
            var iConfigurationManager = compilation.GetTypeByMetadataName(KnownTypeNames.IConfigurationManager);
            var iConfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.IConfiguration);
            var iConfigurationBuilder = compilation.GetTypeByMetadataName(KnownTypeNames.IConfigurationBuilder);
            var iHostEnvironment = compilation.GetTypeByMetadataName(KnownTypeNames.IHostEnvironment);

            if (sendOnlyEndpointAttribute is null
                || endpointConfiguration is null
                || iServiceCollection is null
                || iConfigurationManager is null
                || iConfiguration is null
                || iConfigurationBuilder is null
                || iHostEnvironment is null)
            {
                knownTypes = default;
                return false;
            }

            knownTypes = new SendOnlyEndpointGeneratorKnownTypes(
                sendOnlyEndpointAttribute,
                endpointConfiguration,
                iServiceCollection,
                iConfigurationManager,
                iConfiguration,
                iConfigurationBuilder,
                iHostEnvironment);

            return true;
        }
    }
}