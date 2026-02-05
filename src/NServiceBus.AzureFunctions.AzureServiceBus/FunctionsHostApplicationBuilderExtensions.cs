namespace NServiceBus;

using System;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBus(
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

        transport.RegisterServices(builder.Services, endpointName);
    }
}
