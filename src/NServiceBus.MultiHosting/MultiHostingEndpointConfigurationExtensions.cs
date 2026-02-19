namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Extensions.DependencyInjection;
using MultiHosting.Services;

public static class MultiHostingEndpointConfigurationExtensions
{
    extension(EndpointConfiguration endpointConfiguration)
    {
        /// <summary>
        /// Provides access to hosting related configuration for multi-hosted endpoints.
        /// </summary>
        public HostConfiguration Hosting => endpointConfiguration.GetSettings().Get<HostConfiguration>();

        /// <summary>
        /// Gets the endpoint specific collection of services.
        /// </summary>
        public IServiceCollection EndpointSpecificServices => endpointConfiguration.GetSettings().Get<KeyedServiceCollectionAdapter>();
    }
}