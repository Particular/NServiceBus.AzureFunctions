namespace NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public delegate void MultiEndpointConfiguration(
    EndpointConfiguration endpoint,
    IServiceCollection services,
    IConfiguration configuration,
    IHostEnvironment environment);