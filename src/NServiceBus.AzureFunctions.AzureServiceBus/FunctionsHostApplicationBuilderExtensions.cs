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
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(functionManifest);

        builder.Services.AddAzureClientsCore();

        var endpointName = functionManifest.Name;
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        functionManifest.Configuration(endpointConfiguration, builder.Configuration, builder.Environment);

        var settings = endpointConfiguration.GetSettings();
        if (settings.GetOrDefault<bool>(AzureServiceBusServerlessTransport.SendOnlyConfigKey))
        {
            throw new InvalidOperationException($"Functions can't be send only endpoints, use {nameof(AddSendOnlyNServiceBusEndpoint)}");
        }

        var transport = settings.TryGet(out TransportDefinition configuredTransport)
            ? configuredTransport as AzureServiceBusServerlessTransport
            : throw new InvalidOperationException($"{nameof(AzureServiceBusServerlessTransport)} needs to be configured");

        if (transport is null)
        {
            throw new InvalidOperationException($"Endpoint must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
        }

        if (functionManifest.Name != functionManifest.Queue)
        {
            endpointConfiguration.OverrideLocalAddress(functionManifest.Queue);
        }

        transport.ConnectionName = functionManifest.ConnectionName;
        builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
        builder.Services.AddKeyedSingleton<MessageProcessor>(endpointName, (_, _) => new MessageProcessor(transport, endpointName));
    }

    public static void AddSendOnlyNServiceBusEndpoint(this FunctionsApplicationBuilder builder, string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;
        configure(endpointConfiguration);
        endpointConfiguration.SendOnly();

        var settings = endpointConfiguration.GetSettings();
        if (!settings.TryGet(out TransportDefinition transport) || transport is not AzureServiceBusServerlessTransport)
        {
            throw new InvalidOperationException($"Endpoint must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
        }

        builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
    }
}