namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Transport;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBusFunction(
        this FunctionsApplicationBuilder builder,
        string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddAzureClientsCore();

        builder.AddNServiceBusEndpoint(endpointName, c =>
        {
            configure(c);

            if (!c.GetSettings().TryGet(out TransportDefinition transport))
            {
                throw new InvalidOperationException("No transport has been defined.");
            }

            if (transport is not AzureServiceBusServerlessTransport serverlessTransport)
            {
                throw new InvalidOperationException($"Endpoint '{endpointName}' must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
            }

            builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) => new MessageProcessor(serverlessTransport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName)));
        });
    }
}