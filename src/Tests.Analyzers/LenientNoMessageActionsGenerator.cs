namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using NServiceBus.AzureFunctions.Analyzer;
using NServiceBus.Core.Analyzer;

/// <summary>
/// Test generator that exercises a transport with additional parameters.
/// Uses a custom trigger attribute with transport-specific parameters.
/// </summary>
class LenientNoMessageActionsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => FunctionEndpointGenerator.InitializeGenerator(context,
            new FunctionEndpointGenerator.TriggerDefinition(
                TriggerAttributeMetadataName: "Demo.Testing.TestTriggerAttribute",
                AdditionalParameterTypes: ImmutableEquatableArray<FunctionEndpointGenerator.AdditionalParameterType>.Empty,
                ProcessorTypeFullyQualified: "global::Demo.Testing.TestProcessor",
                AddressExtraction: FunctionEndpointGenerator.AddressExtractionPolicy.FromNamedConstructorParameter("queueName"),
                ConnectionSetting: FunctionEndpointGenerator.ConnectionSettingPolicy.FromNamedProperty("ConnSetting"),
                AutoComplete: FunctionEndpointGenerator.AutoCompletePolicy.None,
                RegistrationMethodFullyQualified: "global::Demo.Testing.TestFunctionManifestRegistration.Register",
                ProcessMethodName: "Process",
                Shape: FunctionEndpointGenerator.TriggerShape.RequiredAllowingAdditionalParameters(
                    FunctionEndpointGenerator.ParameterRole.TriggerMessage,
                    FunctionEndpointGenerator.ParameterRole.FunctionContext,
                    FunctionEndpointGenerator.ParameterRole.CancellationToken)),
            new FunctionEndpointGenerator.SendOnlyEndpointDefinition("global::Demo.Testing.TestSendOnlyEndpointManifestRegistration.Register"));

    internal static class TrackingNames
    {
        public const string Extraction = nameof(Extraction);
        public const string Diagnostics = nameof(Diagnostics);
        public const string Functions = nameof(Functions);
        public const string AssemblyClassName = nameof(AssemblyClassName);
        public const string Combined = nameof(Combined);

        public static string[] All => [Extraction, Diagnostics, Functions, AssemblyClassName, Combined];
    }
}
