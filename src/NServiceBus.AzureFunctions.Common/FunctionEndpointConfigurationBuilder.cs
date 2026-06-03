namespace NServiceBus;

using Configuration.AdvancedExtensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Utils;

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
    /// <exception cref="InvalidOperationException">Thrown when the supplied manifest configures the endpoint as send-only.</exception>
    public static EndpointConfiguration BuildReceiveEndpointConfiguration(
        FunctionsApplicationBuilder builder,
        FunctionManifest functionManifest)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(functionManifest);

        var endpointName = functionManifest.Name;
        var endpointConfiguration = CreateDefaultEndpointConfiguration(
            endpointName,
            builder,
            (configuration, endpointServices) => functionManifest.Configuration(configuration, endpointServices, builder.Configuration, builder.Environment));

        if (endpointConfiguration.IsSendOnly)
        {
            throw new InvalidOperationException($"Functions can't be send-only endpoints, use [{typeof(NServiceBusSendOnlyEndpointAttribute)}] to create send-only endpoints.");
        }

        var resolvedAddress = FunctionBindingExpression.Resolve(functionManifest.Address, builder.Configuration);
        if (functionManifest.Name != resolvedAddress)
        {
            endpointConfiguration.OverrideLocalAddress(resolvedAddress);
        }

        return endpointConfiguration;
    }

    /// <summary>
    /// Builds a send-only <see cref="EndpointConfiguration"/> from the supplied <paramref name="manifest"/>.
    /// </summary>
    /// <param name="builder">The Functions application builder the endpoint is attached to.</param>
    /// <param name="manifest">The manifest describing the send-only endpoint to configure.</param>
    public static EndpointConfiguration BuildSendOnlyEndpointConfiguration(
        FunctionsApplicationBuilder builder,
        SendOnlyEndpointManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(manifest);

        var endpointConfiguration = CreateDefaultEndpointConfiguration(
            manifest.Name,
            builder,
            (configuration, endpointServices) => manifest.Configuration(configuration, endpointServices, builder.Configuration, builder.Environment));

        endpointConfiguration.SendOnly();

        return endpointConfiguration;
    }

    static EndpointConfiguration CreateDefaultEndpointConfiguration(string endpointName, FunctionsApplicationBuilder builder, Action<EndpointConfiguration, IServiceCollection> userEndpointConfiguration)
    {
        if (!AppContext.TryGetSwitch(UseV2DeterministicGuidAppSwitchKey, out _))
        {
            AppContext.SetSwitch(UseV2DeterministicGuidAppSwitchKey, true);
        }

        var endpointConfiguration = new EndpointConfiguration(endpointName);
        endpointConfiguration.AssemblyScanner().Disable = true;

        var hostIdentifier = ResolveDefaultHostIdentifier(builder.Configuration);

        endpointConfiguration.UniquelyIdentifyRunningInstance()
            .UsingCustomDisplayName(hostIdentifier)
            .UsingCustomIdentifier(DeterministicGuid.Create(hostIdentifier));

        endpointConfiguration.CustomDiagnosticsWriter(NoOpDiagnosticsWriter);

        var settings = endpointConfiguration.GetSettings();
        var endpointServices = settings.GetOrCreateKeyedServiceCollection(builder.Services, endpointName);

        userEndpointConfiguration(endpointConfiguration, endpointServices);

        return endpointConfiguration;
    }

    static Task NoOpDiagnosticsWriter(string diagnostics, CancellationToken cancellationToken) => Task.CompletedTask;

    static string ResolveDefaultHostIdentifier(IConfiguration configuration)
    {
        // this would be set if running inside a function app
        var websiteInstanceId = configuration[WebsiteInstanceIdKey];
        if (!string.IsNullOrWhiteSpace(websiteInstanceId))
        {
            return websiteInstanceId;
        }

        // this would be set if running inside a container app
        var containerName = configuration[ContainerNameKey];
        if (!string.IsNullOrWhiteSpace(containerName))
        {
            return containerName;
        }

        // fallback to machine name for local development
        return Environment.MachineName;
    }

    const string WebsiteInstanceIdKey = "WEBSITE_INSTANCE_ID";
    const string ContainerNameKey = "CONTAINER_NAME";
    const string UseV2DeterministicGuidAppSwitchKey = "NServiceBus.Core.Hosting.UseV2DeterministicGuid";
}