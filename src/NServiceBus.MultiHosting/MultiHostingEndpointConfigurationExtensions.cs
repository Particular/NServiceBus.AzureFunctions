namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class MultiHostingEndpointConfigurationExtensions
{
    extension(EndpointConfiguration endpointConfiguration)
    {
        /// <summary>
        /// Gets the set of key/value configuration properties.
        /// </summary>
        /// <remarks>
        /// This can be mutated by adding more configuration sources, which will update its current view.
        /// </remarks>
        public IConfigurationManager Configuration => endpointConfiguration.GetSettings().Get<IHostApplicationBuilder>().Configuration;

        /// <summary>
        /// Gets the information about the hosting environment an application is running in.
        /// </summary>
        public IHostEnvironment Environment => endpointConfiguration.GetSettings().Get<IHostApplicationBuilder>().Environment;

        /// <summary>
        /// Gets a collection of services for the application to compose. This is useful for adding user provided or framework provided services.
        /// </summary>
        public IServiceCollection Services => endpointConfiguration.GetSettings().Get<IServiceCollection>();
    }
}