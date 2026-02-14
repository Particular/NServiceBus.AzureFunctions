using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus.MultiHosting;

public class InitializeLogger(ILoggerFactory loggerFactory) : IHostedLifecycleService
{
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StartingAsync(CancellationToken cancellationToken = default)
    {
        MultiEndpointLoggerFactory.Instance.SetLoggerFactory(loggerFactory);
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
