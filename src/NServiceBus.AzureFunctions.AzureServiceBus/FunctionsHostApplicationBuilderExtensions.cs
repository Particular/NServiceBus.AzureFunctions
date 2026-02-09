namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBusFunction(
        this FunctionsApplicationBuilder builder,
        string endpointName,
        AzureServiceBusServerlessTransport transport,
        Action<EndpointConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(transport);

        builder.Services.AddAzureClientsCore();

        builder.Services.AddNServiceBusEndpoint(endpointName, endpoint =>
        {
            endpoint.UseTransport(transport);
            configure(endpoint);
        });

        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
            new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName)));
    }
}
