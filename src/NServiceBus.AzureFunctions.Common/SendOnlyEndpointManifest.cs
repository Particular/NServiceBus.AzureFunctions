namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

/// <summary>
/// Describes a send-only NServiceBus endpoint hosted in Azure Functions. Produced by the source generator
/// and passed to the transport-specific registration method referenced by <see cref="Register"/>.
/// Should only be created by the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
/// <param name="Name">The endpoint name.</param>
/// <param name="ConnectionSettingName">The name of the application setting or configuration section that contains the transport connection details, or <see langword="null" /> to use the transport's default.</param>
/// <param name="Configuration">Callback invoked to customize the endpoint configuration and its service registrations.</param>
/// <param name="Register">Transport-specific callback that registers the endpoint with the Functions host builder.</param>
public sealed record SendOnlyEndpointManifest(
    string Name,
    string? ConnectionSettingName,
    FunctionEndpointConfiguration Configuration,
    Action<FunctionsApplicationBuilder, SendOnlyEndpointManifest> Register) : IConnectionSettingManifest;