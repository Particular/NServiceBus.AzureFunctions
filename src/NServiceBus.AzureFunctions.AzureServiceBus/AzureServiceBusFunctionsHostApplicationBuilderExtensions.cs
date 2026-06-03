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

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildReceiveEndpointConfiguration(builder, functionManifest, $"[{nameof(NServiceBusSendOnlyEndpointAttribute)}]");
        var transport = GetAzureServiceBusTransport(endpointConfiguration);

        var resolvedConnectionSettingName = string.IsNullOrWhiteSpace(functionManifest.ConnectionSettingName)
            ? functionManifest.ConnectionSettingName
            : FunctionBindingExpression.Resolve(functionManifest.ConnectionSettingName, builder.Configuration);
        transport.ConnectionName = resolvedConnectionSettingName;
        builder.Services.AddNServiceBusEndpoint(endpointConfiguration, endpointConfiguration.EndpointName);
        builder.Services.AddKeyedSingleton<AzureServiceBusMessageProcessor>(functionManifest.Name, (_, _) => new AzureServiceBusMessageProcessor(transport, functionManifest.Name));
    }

    /// <summary>
    /// Adds the necessary services to the container to support the send-only endpoint described by the provided <see cref="SendOnlyEndpointManifest"/>.
    /// Should only be called by the source generator.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void AddNServiceBusAzureServiceBusSendOnlyEndpoint(this FunctionsApplicationBuilder builder, SendOnlyEndpointManifest sendOnlyEndpointManifest)
    {
        builder.Services.AddAzureClientsCore();

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(builder, sendOnlyEndpointManifest);
        var transport = GetAzureServiceBusTransport(endpointConfiguration);

        if (!string.IsNullOrWhiteSpace(transport.ConnectionName))
        {
            transport.ConnectionName = FunctionBindingExpression.Resolve(transport.ConnectionName, builder.Configuration);
        }

        builder.Services.AddNServiceBusEndpoint(endpointConfiguration, endpointConfiguration.EndpointName);
    }

    static AzureServiceBusServerlessTransport GetAzureServiceBusTransport(EndpointConfiguration endpointConfiguration)
    {
        var transport = endpointConfiguration.GetSettings().GetOrDefault<TransportDefinition>() as AzureServiceBusServerlessTransport;

        return transport ?? throw new InvalidOperationException($"Endpoint '{endpointConfiguration.EndpointName}' must be configured with an '{nameof(AzureServiceBusServerlessTransport)}'.");
    }
}