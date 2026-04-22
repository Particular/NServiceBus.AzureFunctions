namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

/// <summary>
/// Describes an NServiceBus endpoint hosted in Azure Functions. Produced by the source generator
/// and passed to the transport-specific registration method referenced by <see cref="Register"/>.
/// Should only be created by the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
/// <param name="Name">The endpoint name.</param>
/// <param name="Address">The transport address the trigger reads messages from (for example, a Service Bus queue or topic). May differ from <paramref name="Name"/>.</param>
/// <param name="ConnectionSettingName">The configuration key whose value resolves to the transport connection (for example, a connection string or fully-qualified namespace).</param>
/// <param name="Configuration">Callback invoked to customize the endpoint configuration and its service registrations.</param>
/// <param name="Register">Transport-specific callback that registers the endpoint with the Functions host builder.</param>
public sealed record FunctionManifest(string Name, string Address, string ConnectionSettingName, FunctionEndpointConfiguration Configuration, Action<FunctionsApplicationBuilder, FunctionManifest> Register);
