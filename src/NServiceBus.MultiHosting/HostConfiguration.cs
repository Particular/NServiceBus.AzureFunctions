namespace NServiceBus;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosting related configuration for multi-hosted endpoints.
/// </summary>
/// <param name="builder"></param>
public class HostConfiguration(IHostApplicationBuilder builder)
{
    /// <summary>
    /// Gets the set of key/value configuration properties.
    /// </summary>
    public IConfiguration Configuration => builder.Configuration;

    /// <summary>
    /// Gets the information about the hosting environment an application is running in.
    /// </summary>
    public IHostEnvironment Environment => builder.Environment;
}