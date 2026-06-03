namespace NServiceBus.AzureFunctions.Analyzer;

using NServiceBus.Core.Analyzer;

public sealed partial class FunctionEndpointGenerator
{
    internal sealed record AzureServiceBusTriggerDefinition() : TriggerDefinition(TriggerAttributeMetadataName: "Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute",
        AdditionalParameterTypes: new AdditionalParameterType[] { new("Microsoft.Azure.Functions.Worker.ServiceBusMessageActions", MessageActions) }.ToImmutableEquatableArray(),
        ProcessorTypeFullyQualified: "global::NServiceBus.AzureFunctions.AzureServiceBus.AzureServiceBusMessageProcessor",
        AddressExtraction: AddressExtractionPolicy.FromNamedConstructorParameter("queueName"),
        ConnectionSetting: ConnectionSettingPolicy.FromNamedProperty("Connection"),
        AutoComplete: AutoCompletePolicy.MustBeFalseFor("AutoCompleteMessages"),
        RegistrationMethodFullyQualified: $"global::{KnownTypeNames.AzureServiceBusFunctionsHostApplicationBuilderExtensions}.{KnownTypeNames.AddNServiceBusAzureServiceBusFunction}",
        ProcessMethodName: "Process",
        Shape: TriggerShape.Required(
            ParameterRole.TriggerMessage,
            MessageActions,
            ParameterRole.FunctionContext,
            ParameterRole.CancellationToken))
    {
        static readonly ParameterRole MessageActions = new("MessageActions");
    }
}