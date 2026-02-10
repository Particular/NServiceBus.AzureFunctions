namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Settings;

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
            var transport = sp.GetRequiredKeyedService<IReadOnlySettings>(endpointName).Get<AzureServiceBusServerlessTransport>();
            return new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName));
        });
    }
}
