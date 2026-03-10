namespace NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public delegate void FunctionEndpointConfiguration(
    EndpointConfiguration endpoint,
    IConfiguration configuration,
    IHostEnvironment environment);