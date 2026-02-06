namespace NServiceBus;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.MultiHosting;
using NServiceBus.MultiHosting.Services;

public static class ServiceCollectionExtensions
{
    public static void AddNServiceBusEndpoint(
        this IServiceCollection services,
        string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        using var _ = MultiEndpointLoggerFactory.Instance.PushName(endpointName);

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        configure(endpointConfiguration);

        var keyedServices = new KeyedServiceCollectionAdapter(services, endpointName);
        var startableEndpoint = EndpointWithExternallyManagedContainer.Create(
            endpointConfiguration, keyedServices);

        services.AddKeyedSingleton(endpointName, (sp, _) =>
            new EndpointStarter(startableEndpoint, sp, endpointName, keyedServices));

        services.AddSingleton<IHostedService, NServiceBusHostedService>(sp =>
            new NServiceBusHostedService(sp.GetRequiredKeyedService<EndpointStarter>(endpointName)));

        services.AddKeyedSingleton<IMessageSession>(endpointName, (sp, key) =>
            new HostAwareMessageSession(sp.GetRequiredKeyedService<EndpointStarter>(key)));
    }
}
