namespace NServiceBus.MultiHosting;

/// <summary>
/// Interface for managing the lifecycle of an NServiceBus endpoint in multi-endpoint scenarios.
/// This abstraction allows the lifecycle classes (NServiceBusHostedService, HostAwareMessageSession)
/// to remain transport-agnostic while the implementation can be transport-specific.
/// </summary>
public interface IEndpointStarter : IAsyncDisposable
{
    /// <summary>
    /// Gets the service key used to identify this endpoint in keyed service registration.
    /// </summary>
    string ServiceKey { get; }

    /// <summary>
    /// Gets or starts the endpoint. This method is idempotent - calling it multiple times
    /// will return the same endpoint instance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The started endpoint instance.</returns>
    ValueTask<IEndpointInstance> GetOrStart(CancellationToken cancellationToken = default);
}
