namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Settings;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBusFunction(
        this FunctionsApplicationBuilder builder,
        string endpointName,
        AzureServiceBusServerlessTransport transport,
        Action<EndpointConfiguration> configure,
        Action<RoutingSettings<AzureServiceBusServerlessTransport>>? routingConfig = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(transport);

        builder.Services.AddAzureClientsCore();

        builder.Services.AddNServiceBusEndpoint(endpointName, endpoint =>
        {
            var routing = endpoint.UseTransport(transport);

            routingConfig?.Invoke(routing);

            configure(endpoint);

            endpoint.GetSettings().Set("MultiHost.UseInEndpoint", endpointName);

        });

        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
            new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName)));
    }

    public static void AddNServiceBusFunctionAltB(
        this FunctionsApplicationBuilder builder,
        string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddAzureClientsCore();

        builder.Services.AddNServiceBusEndpoint(endpointName, configure);

        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
        {
            var transport = sp.GetRequiredKeyedService<IReadOnlySettings>(endpointName).Get<AzureServiceBusServerlessTransport>();
            return new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName));
        });
    }
}