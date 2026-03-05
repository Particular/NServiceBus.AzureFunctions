namespace NServiceBus.MultiHosting;

using Microsoft.Extensions.Hosting;

/// <summary>
/// Hosted service that starts NServiceBus endpoints in multi-endpoint scenarios.
/// </summary>
public sealed class NServiceBusHostedService(IEndpointStarter endpointStarter) : IHostedLifecycleService, IAsyncDisposable
{
    public async Task StartingAsync(CancellationToken cancellationToken = default) => await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await endpointStarter.DisposeAsync().ConfigureAwait(false);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
