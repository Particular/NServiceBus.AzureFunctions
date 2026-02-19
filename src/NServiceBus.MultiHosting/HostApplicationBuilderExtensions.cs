namespace NServiceBus;

using System.Runtime.CompilerServices;
using Configuration.AdvancedExtensibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiHosting;
using MultiHosting.Services;
using Transport;

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

        var settings = endpointConfiguration.GetSettings();

        var keyedServices = new KeyedServiceCollectionAdapter(builder.Services, endpointName);

        settings.Set<IServiceCollection>(keyedServices);
        settings.Set(builder);

        configure(endpointConfiguration);

        var transport = endpointConfiguration.GetSettings().Get<TransportDefinition>();
        var transportKey = $"NServiceBus.Transport.{RuntimeHelpers.GetHashCode(transport)}";
        if (builder.Properties.TryGetValue(transportKey, out var existingEndpoint))
        {
            throw new InvalidOperationException(
                $"This transport instance is already used by endpoint '{existingEndpoint}'. Each endpoint requires its own transport instance.");
        }

        builder.Properties[transportKey] = endpointName;

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