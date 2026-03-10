namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Settings;
using Transport;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBusFunction(this FunctionsApplicationBuilder builder, FunctionManifest functionManifest)
    {
        builder.Services.AddAzureClientsCore();

        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildReceiveEndpointConfiguration(builder, functionManifest, nameof(AddSendOnlyNServiceBusEndpoint));
        var transport = GetAzureServiceBusTransport(endpointConfiguration.GetSettings());

        transport.ConnectionName = functionManifest.ConnectionName;
        builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
        builder.Services.AddKeyedSingleton<MessageProcessor>(functionManifest.Name, (_, _) => new MessageProcessor(transport, functionManifest.Name));
    }

    public static void AddSendOnlyNServiceBusEndpoint(this FunctionsApplicationBuilder builder, string endpointName,
        Action<EndpointConfiguration> configure)
    {
        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(builder, endpointName, configure);
        _ = GetAzureServiceBusTransport(endpointConfiguration.GetSettings());

        builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
    }

    static AzureServiceBusServerlessTransport GetAzureServiceBusTransport(SettingsHolder settings)
    {
        var transport = settings.TryGet(out TransportDefinition configuredTransport)
            ? configuredTransport as AzureServiceBusServerlessTransport
            : throw new InvalidOperationException($"{nameof(AzureServiceBusServerlessTransport)} needs to be configured");

        if (transport is null)
        {
            throw new InvalidOperationException($"Endpoint must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
        }

        return transport;
    }    
}