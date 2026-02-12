namespace NServiceBus;

using System.Linq;
using AzureFunctions.AzureServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class FunctionConfigurationValidator(
    IEnumerable<ExpectedNServiceBusFunction> expectedFunctions,
    IServiceProvider serviceProvider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var unconfigured = expectedFunctions
            .Where(f => serviceProvider.GetKeyedService<IMessageProcessor>(f.Name) is null)
            .Select(f => f.Name)
            .ToArray();

        if (unconfigured.Length > 0)
        {
            throw new InvalidOperationException(
                $"The following NServiceBus functions have not been configured using " +
                $"{nameof(FunctionsHostApplicationBuilderExtensions.AddNServiceBusFunction)}(...): " +
                $"{string.Join(", ", unconfigured)}");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class ExpectedNServiceBusFunction(string name)
{
    public string Name => name;
}