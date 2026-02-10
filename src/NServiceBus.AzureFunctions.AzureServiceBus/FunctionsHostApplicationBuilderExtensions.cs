namespace NServiceBus;

using System;
using System.Runtime.CompilerServices;
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

        var transportKey = $"NServiceBus.Transport.{RuntimeHelpers.GetHashCode(transport)}";
        if (builder.Properties.TryGetValue(transportKey, out var existingEndpoint))
        {
            throw new InvalidOperationException(
                $"This transport instance is already used by endpoint '{existingEndpoint}'. Each endpoint requires its own transport instance.");
        }
        builder.Properties[transportKey] = endpointName;

        builder.Services.AddAzureClientsCore();

        builder.AddNServiceBusEndpoint(endpointName, endpoint =>
        {
            endpoint.UseTransport(transport);
            configure(endpoint);
        });

        builder.Services.AddKeyedSingleton<IMessageProcessor>(endpointName, (sp, _) =>
            new MessageProcessor(transport, sp.GetRequiredKeyedService<MultiHosting.EndpointStarter>(endpointName)));
    }
}
