namespace NServiceBus.AzureFunctions.Analyzer;

public sealed partial class SendOnlyEndpointGenerator
{
    static readonly SendOnlyEndpointDefinition AzureServiceBusSendOnlyEndpoint = new(
        RegistrationMethodFullyQualified: $"global::{KnownTypeNames.AzureServiceBusFunctionsHostApplicationBuilderExtensions}.{KnownTypeNames.AddNServiceBusAzureServiceBusSendOnlyEndpoint}");
}