namespace NServiceBus.MultiHosting;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that starts NServiceBus endpoints in multi-endpoint scenarios.
/// </summary>
public sealed class NServiceBusHostedService(IEndpointStarter endpointStarter) : IHostedService, IAsyncDisposable
{
    public async Task StartAsync(CancellationToken cancellationToken = default)
        => await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await endpointStarter.DisposeAsync().ConfigureAwait(false);
    }
}
