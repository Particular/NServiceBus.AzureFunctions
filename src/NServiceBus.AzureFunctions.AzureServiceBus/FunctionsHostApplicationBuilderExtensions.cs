namespace NServiceBus;

using System;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions methods to configure the FunctionsApplicationBuilder with NServiceBus endpoints.
/// </summary>
public static class FunctionsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds an NServiceBus endpoint to the Azure Functions host. The endpoint will be configured as send-only.
    /// </summary>
    /// <remarks>It is possible to use any transport as send-only with this method but only the Serverless variants like <see cref="AzureServiceBusServerlessTransport"/> will provide first class
    /// functions host integration support.</remarks>
    /// <param name="builder">The functions application builder.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="configure">The configuration action to configure the endpoint configuration.</param>
    public static void AddSendOnlyNServiceBusEndpoint(this FunctionsApplicationBuilder builder, string endpointName,
        Action<EndpointConfiguration> configure) => builder.AddSendOnlyNServiceBusEndpoint(endpointName, (endpointConfiguration, _) => configure(endpointConfiguration));

    /// <summary>
    /// Adds an NServiceBus endpoint to the Azure Functions host. The endpoint will be configured as send-only.
    /// </summary>
    /// <remarks>It is possible to use any transport as send-only with this method but only the Serverless variants like <see cref="AzureServiceBusServerlessTransport"/> will provide first class
    /// functions host integration support.</remarks>
    /// <param name="builder">The functions application builder.</param>
    /// <param name="endpointName">The endpoint name.</param>
    /// <param name="configure">The configuration action to configure the endpoint configuration and the endpoint-specific services, if any.</param>
    public static void AddSendOnlyNServiceBusEndpoint(this FunctionsApplicationBuilder builder, string endpointName,
        Action<EndpointConfiguration, IServiceCollection> configure)
    {
        var endpointConfiguration = FunctionEndpointConfigurationBuilder.BuildSendOnlyEndpointConfiguration(builder, endpointName, configure);

        builder.Services.AddNServiceBusEndpoint(endpointConfiguration, endpointName);
    }
}