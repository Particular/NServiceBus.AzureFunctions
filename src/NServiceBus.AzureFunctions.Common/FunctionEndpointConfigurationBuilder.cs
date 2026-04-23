namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Produces a configured <see cref="EndpointConfiguration"/> for an Azure Functions-hosted endpoint.
/// Used by endpoint-registration extensions and by code emitted by the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
public static class FunctionEndpointConfigurationBuilder
{
    /// <summary>
    /// Builds an <see cref="EndpointConfiguration"/> for a receiving endpoint described by
    /// <paramref name="functionManifest"/>. Should only be called by the source generator.
    /// </summary>
    /// <param name="builder">The Functions application builder the endpoint is attached to.</param>
    /// <param name="functionManifest">The manifest describing the endpoint to configure.</param>
    /// <param name="sendOnlyEndpointApiName">Name of the send-only API included in the exception thrown when a send-only configuration is detected.</param>
    /// <exception cref="InvalidOperationException">Thrown when the supplied manifest configures the endpoint as send-only.</exception>
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

    /// <summary>
    /// Builds a send-only <see cref="EndpointConfiguration"/> with the customizations supplied via
    /// <paramref name="configure"/>.
    /// </summary>
    /// <param name="builder">The Functions application builder the endpoint is attached to.</param>
    /// <param name="endpointName">The logical name of the send-only endpoint.</param>
    /// <param name="configure">Callback invoked to configure the endpoint and register endpoint-specific services.</param>
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