namespace NServiceBus.AzureFunctions.Analyzer;

using NServiceBus.Core.Analyzer;

public sealed partial class FunctionEndpointGenerator
{
    static readonly ParameterRole MessageActions = new("MessageActions");

    static readonly TriggerDefinition AzureServiceBusTrigger = new(
        TriggerAttributeMetadataName: "Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute",
        AdditionalParameterTypes: new AdditionalParameterType[]
        {
            new("Microsoft.Azure.Functions.Worker.ServiceBusMessageActions", MessageActions)
        }.ToImmutableEquatableArray(),
        ProcessorTypeFullyQualified: "global::NServiceBus.AzureFunctions.AzureServiceBus.AzureServiceBusMessageProcessor",
        ConnectionPropertyName: "Connection",
        AutoCompletePropertyName: "AutoCompleteMessages",
        RequireAutoCompleteFalse: true,
        RegistrationMethodFullyQualified: "global::NServiceBus.AzureFunctions.AzureServiceBus.AzureServiceBusFunctionManifestRegistration.Register",
        ProcessMethodName: "Process",
        Shape: TriggerShape.Required(
            ParameterRole.TriggerMessage,
            MessageActions,
            ParameterRole.FunctionContext,
            ParameterRole.CancellationToken));
}
