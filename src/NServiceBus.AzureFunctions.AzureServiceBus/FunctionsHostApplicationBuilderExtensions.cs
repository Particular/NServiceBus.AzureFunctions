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

        var endpointKey = $"NServiceBus.Endpoint.{endpointName}";
        if (builder.Properties.ContainsKey(endpointKey))
        {
            throw new InvalidOperationException(
                $"An endpoint with the name '{endpointName}' has already been registered.");
        }

        builder.Properties[transportKey] = endpointName;
        builder.Properties[endpointKey] = true;

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
