namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

public static class FunctionEndpointConfigurationBuilder
{
    public static EndpointConfiguration BuildReceiveEndpointConfiguration(
        FunctionsApplicationBuilder builder,
        FunctionManifest functionManifest,
        string sendOnlyEndpointApiName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(functionManifest);

        var endpointName = functionManifest.Name;
        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        var settings = endpointConfiguration.GetSettings();
        var endpointServices = settings.GetOrCreateKeyedServiceCollection(builder.Services, endpointName);
        functionManifest.Configuration(endpointConfiguration, endpointServices, builder.Configuration, builder.Environment);

        if (endpointConfiguration.IsSendOnly)
        {
            throw new InvalidOperationException($"Functions can't be send only endpoints, use {sendOnlyEndpointApiName}");
        }

        if (functionManifest.Name != functionManifest.Address)
        {
            endpointConfiguration.OverrideLocalAddress(functionManifest.Address);
        }

        return endpointConfiguration;
    }

    public static EndpointConfiguration BuildSendOnlyEndpointConfiguration(
        FunctionsApplicationBuilder builder,
        string endpointName,
        Action<EndpointConfiguration, IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        var settings = endpointConfiguration.GetSettings();
        var endpointServices = settings.GetOrCreateKeyedServiceCollection(builder.Services, endpointName);

        configure(endpointConfiguration, endpointServices);
        endpointConfiguration.SendOnly();
        return endpointConfiguration;
    }
}