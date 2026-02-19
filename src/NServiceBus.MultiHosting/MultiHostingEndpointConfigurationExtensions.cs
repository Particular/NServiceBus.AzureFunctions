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
        /// Gets a collection of services for the application to compose. This is useful for adding user provided or framework provided services.
        /// </summary>
        public IServiceCollection Services => endpointConfiguration.GetSettings().Get<KeyedServiceCollectionAdapter>();
    }
}