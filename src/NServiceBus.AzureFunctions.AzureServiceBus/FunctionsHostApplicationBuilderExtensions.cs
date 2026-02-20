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
    public static void AddNServiceBusFunction(
        this FunctionsApplicationBuilder builder,
        FunctionManifest functionManifest)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(functionManifest);

        builder.Services.AddAzureClientsCore();

        var endpointName = functionManifest.Name;
        builder.AddNServiceBusEndpoint(endpointName, (endpointConfiguration, services, configuration, hostEnvironment) =>
        {
            functionManifest.Configuration(endpointConfiguration, services, configuration, hostEnvironment);

            var settings = endpointConfiguration.GetSettings();
            if (settings.GetOrDefault<bool>(AzureServiceBusServerlessTransport.SendOnlyConfigKey))
            {
                throw new InvalidOperationException($"Functions can't be send only endpoints, use {nameof(AddSendOnlyNServiceBusEndpoint)}");
            }

            var transport = GetTransport(settings);

            if (functionManifest.Name != functionManifest.Queue)
            {
                endpointConfiguration.OverrideLocalAddress(functionManifest.Queue);
            }

            transport.ConnectionName = functionManifest.ConnectionName;

            builder.Services.AddKeyedSingleton<MessageProcessor>(endpointName, (sp, _) => new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName)));
        });
    }

    public static void AddSendOnlyNServiceBusEndpoint(
        this FunctionsApplicationBuilder builder,
        SendOnlyManifest sendOnlyManifest)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(sendOnlyManifest);

        builder.AddNServiceBusEndpoint(sendOnlyManifest.Name, (endpointConfiguration, services, configuration, hostEnvironment) =>
        {
            sendOnlyManifest.Configuration(endpointConfiguration, services, configuration, hostEnvironment);

            endpointConfiguration.SendOnly();

            // Make sure that the correct transport is used
            _ = GetTransport(endpointConfiguration.GetSettings());
        });
    }

    static AzureServiceBusServerlessTransport GetTransport(SettingsHolder settings)
    {
        if (!settings.TryGet(out TransportDefinition transport))
        {
            throw new InvalidOperationException($"{nameof(AzureServiceBusServerlessTransport)} needs to be configured");
        }

        if (transport is not AzureServiceBusServerlessTransport serverlessTransport)
        {
            throw new InvalidOperationException($"Endpoint must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
        }

        return serverlessTransport;
    }
}