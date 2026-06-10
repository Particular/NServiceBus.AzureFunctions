namespace NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Configures an NServiceBus endpoint hosted in Azure Functions. The source generator emits an
/// instance of this delegate for each function, forwarding to the user's
/// <c>Configure{FunctionName}</c> method on the class that declares the
/// <see cref="NServiceBusFunctionAttribute"/>-marked trigger. This type should only be used by
/// the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
/// <param name="endpoint">The endpoint configuration to customize.</param>
/// <param name="services">The endpoint-scoped service collection.</param>
/// <param name="configuration">The application configuration.</param>
/// <param name="environment">The hosting environment.</param>
public delegate void FunctionEndpointConfiguration(
    EndpointConfiguration endpoint,
    IServiceCollection services,
    IConfigurationManager configuration,
    IHostEnvironment environment);