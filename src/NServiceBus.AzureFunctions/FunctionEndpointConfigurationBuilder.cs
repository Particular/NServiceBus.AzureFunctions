namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;

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

        functionManifest.Configuration(endpointConfiguration, builder.Configuration, builder.Environment);

        var settings = endpointConfiguration.GetSettings();
        if (settings.GetOrDefault<bool>(SendOnlyConfigKey))
        {
            throw new InvalidOperationException($"Functions can't be send only endpoints, use {sendOnlyEndpointApiName}");
        }

        if (functionManifest.Name != functionManifest.Queue)
        {
            endpointConfiguration.OverrideLocalAddress(functionManifest.Queue);
        }

        return endpointConfiguration;
    }

    public static EndpointConfiguration BuildSendOnlyEndpointConfiguration(
        FunctionsApplicationBuilder builder,
        string endpointName,
        Action<EndpointConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(endpointName);
        ArgumentNullException.ThrowIfNull(configure);

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;
        configure(endpointConfiguration);
        endpointConfiguration.SendOnly();
        return endpointConfiguration;
    }

    const string SendOnlyConfigKey = "Endpoint.SendOnly";
}