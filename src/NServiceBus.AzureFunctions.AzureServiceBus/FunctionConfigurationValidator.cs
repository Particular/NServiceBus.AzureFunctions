namespace NServiceBus;

using Microsoft.Extensions.Hosting;

public class FunctionConfigurationValidator : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var allFunctions = FunctionsRegistry.GetAll();

        var functionNotConfigured = allFunctions.Where(f => !f.Configured).Select(f => f.Name).ToArray();
        if (functionNotConfigured.Any())
        {
            throw new InvalidOperationException($"The following functions have not been configured using {nameof(FunctionsHostApplicationBuilderExtensions.AddNServiceBusFunction)}(...): {string.Join(", ", functionNotConfigured)}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}