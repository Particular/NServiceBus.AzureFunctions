namespace NServiceBus.Configuration.AdvancedExtensibility;

using System.ComponentModel;
using AzureFunctions.AzureServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Transport;

/// <summary>
/// Infrastructure extensions that should only ever be called by the source generator.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AzureServiceBusFunctionsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the necessary services to the container to support the function described by the provided <see cref="FunctionManifest"/>. Should only be called by the service Generator
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddNServiceBusAzureServiceBusFunction(this FunctionsApplicationBuilder builder, FunctionManifest functionManifest)
    {
        builder.Services.AddAzureClientsCore();

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildReceiveEndpointConfiguration(builder, functionManifest, nameof(FunctionsHostApplicationBuilderExtensions.AddSendOnlyNServiceBusEndpoint));
        var transport = GetAzureServiceBusTransport(endpointConfiguration);

        transport.ConnectionName = functionManifest.ConnectionSettingName;
        builder.Services.AddNServiceBusEndpoint(endpointConfiguration, endpointConfiguration.EndpointName);
        builder.Services.AddKeyedSingleton<AzureServiceBusMessageProcessor>(functionManifest.Name, (_, _) => new AzureServiceBusMessageProcessor(transport, functionManifest.Name));
    }

    static AzureServiceBusServerlessTransport GetAzureServiceBusTransport(EndpointConfiguration endpointConfiguration)
    {
        var transport = endpointConfiguration.GetSettings().GetOrDefault<TransportDefinition>() as AzureServiceBusServerlessTransport;

        return transport ?? throw new InvalidOperationException($"Endpoint '{endpointConfiguration.EndpointName}' must be configured with an '{nameof(AzureServiceBusServerlessTransport)}'.");
    }
}