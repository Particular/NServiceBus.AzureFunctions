namespace NServiceBus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.MultiHosting;
using NServiceBus.MultiHosting.Services;

public static class HostApplicationBuilderExtensions
{
    public static void AddNServiceBusEndpoint(
        this IHostApplicationBuilder builder,
        string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        var endpointKey = $"NServiceBus.Endpoint.{endpointName}";
        if (builder.Properties.ContainsKey(endpointKey))
        {
            throw new InvalidOperationException(
                $"An endpoint with the name '{endpointName}' has already been registered.");
        }
        builder.Properties[endpointKey] = true;

        using var _ = MultiEndpointLoggerFactory.Instance.PushName(endpointName);

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        configure(endpointConfiguration);

        var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, endpointName);
        var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
            endpointConfiguration, keyedServices);

        builder.Services.AddKeyedSingleton(endpointName, (sp, _) =>
            new EndpointStarter(startableEndpoint, sp, endpointName, keyedServices));

        builder.Services.AddSingleton<IHostedService, NServiceBusHostedService>(sp =>
            new NServiceBusHostedService(sp.GetRequiredKeyedService<EndpointStarter>(endpointName)));

        builder.Services.AddKeyedSingleton<IMessageSession>(endpointName, (sp, key) =>
            new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));
    }
}
