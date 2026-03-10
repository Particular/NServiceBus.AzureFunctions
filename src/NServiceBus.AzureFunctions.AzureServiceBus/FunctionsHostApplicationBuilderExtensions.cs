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
    extension(FunctionsApplicationBuilder builder)
    {
        public void AddNServiceBusFunction(FunctionManifest functionManifest)
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

            var transport = GetTransport(settings);

            if (functionManifest.Name != functionManifest.Queue)
            {
                endpointConfiguration.OverrideLocalAddress(functionManifest.Queue);
            }

            transport.ConnectionName = functionManifest.ConnectionName;
            builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
            builder.Services.AddKeyedSingleton<MessageProcessor>(endpointName, (_, _) => new MessageProcessor(transport, endpointName));
        }

        public void AddSendOnlyNServiceBusEndpoint(string endpointName,
            Action<EndpointConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(endpointName);
            ArgumentNullException.ThrowIfNull(configure);

            var endpointConfiguration = new EndpointConfiguration(endpointName);
            endpointConfiguration.AssemblyScanner().Disable = true;
            configure(endpointConfiguration);
            endpointConfiguration.SendOnly();

            // Make sure that the correct transport is used
            _ = GetTransport(endpointConfiguration.GetSettings());

            builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
        }
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