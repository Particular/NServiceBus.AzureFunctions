namespace NServiceBus;

using System;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AzureFunctions;
using NServiceBus.MultiHosting;
using NServiceBus.MultiHosting.Services;

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBus(
        this FunctionsApplicationBuilder builder,
        string endpointName,
        Action<ServerlessEndpointConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddAzureClientsCore();

        using var _ = FunctionsLoggerFactory.Instance.PushName(endpointName);

        var serverlessConfig = new ServerlessEndpointConfiguration(endpointName);

        configure(serverlessConfig);

        if (serverlessConfig.Transport == null)
        {
            throw new InvalidOperationException(
                $"Transport has not been configured for endpoint '{endpointName}'. " +
                $"Call 'UseTransport' with a serverless transport implementation.");
        }

        var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, endpointName);
        var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
            serverlessConfig.EndpointConfiguration,
            keyedServices);

        builder.Services.AddKeyedSingleton(endpointName, (sp, _) =>
            new EndpointStarter(startableEndpoint, sp, serverlessConfig.Transport, endpointName, keyedServices));

        builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(sp =>
            new NServiceBusHostedService(sp.GetRequiredKeyedService<EndpointStarter>(endpointName)));

        builder.Services.AddKeyedSingleton<IMessageSession>(endpointName, (sp, key) =>
            new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));

        serverlessConfig.Transport.RegisterServices(builder.Services, endpointName);
    }
}