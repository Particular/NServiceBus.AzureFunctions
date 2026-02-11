namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Settings;
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

        builder.AddNServiceBusEndpoint(endpointName, configure);

        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
        {
            var settings = sp.GetRequiredKeyedService<IReadOnlySettings>(endpointName);
            var transport = settings.Get<TransportDefinition>() as AzureServiceBusServerlessTransport
                ?? throw new InvalidOperationException($"Endpoint '{endpointName}' must be configured with an AzureServiceBusServerlessTransport.");
            return new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName));
        });
    }
}