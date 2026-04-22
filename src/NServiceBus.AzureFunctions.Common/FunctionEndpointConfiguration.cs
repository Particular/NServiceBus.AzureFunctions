namespace NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Configures an NServiceBus endpoint hosted in Azure Functions. The source generator wraps methods
/// marked with <see cref="NServiceBusFunctionAttribute"/> in a delegate of this type.
/// </summary>
/// <remarks>The API surface might change between versions according to the needs of the source generator.</remarks>
/// <param name="endpoint">The endpoint configuration to customize.</param>
/// <param name="services">The endpoint-scoped service collection.</param>
/// <param name="configuration">The application configuration.</param>
/// <param name="environment">The hosting environment.</param>
public delegate void FunctionEndpointConfiguration(
    EndpointConfiguration endpoint,
    IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment);