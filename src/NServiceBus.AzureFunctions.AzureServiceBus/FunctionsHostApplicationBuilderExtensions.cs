namespace NServiceBus;

using System;
using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Settings;
using Transport;

/// <summary>
/// Extensions methods to configure the FunctionsApplicationBuilder with NServiceBus endpoints.
/// </summary>
public static class FunctionsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds an NServiceBus endpoint to the Azure Functions host. The endpoint will be configured as send-only.
    /// </summary>
    /// <param name="builder">The functions application builder.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="configure">The configuration action to configure the endpoint configuration.</param>
    public static void AddSendOnlyNServiceBusEndpoint(this FunctionsApplicationBuilder builder, string endpointName,
        Action<EndpointConfiguration> configure)
    {
        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(builder, endpointName, configure);
        _ = GetAzureServiceBusTransport(endpointConfiguration.GetSettings());

        builder.Services.AddNServiceBusEndpoint(endpointConfiguration, endpointName);
    }

    internal static AzureServiceBusServerlessTransport GetAzureServiceBusTransport(SettingsHolder settings)
    {
        var transport = settings.TryGet(out TransportDefinition configuredTransport)
            ? configuredTransport as AzureServiceBusServerlessTransport
            : throw new InvalidOperationException($"{nameof(AzureServiceBusServerlessTransport)} needs to be configured");

        return transport ?? throw new InvalidOperationException($"Endpoint must be configured with an {nameof(AzureServiceBusServerlessTransport)}.");
    }
}
