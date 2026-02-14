namespace NServiceBus;

using System.ComponentModel;
using Logging;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using MultiHosting;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class NServiceBusFunctionsInfrastructure
{
    public static void Initialize(FunctionsApplicationBuilder builder)
    {
        LogManager.UseFactory(MultiEndpointLoggerFactory.Instance);
        builder.Services.AddHostedService<InitializeLogger>();
        builder.Services.AddAzureClientsCore();
    }
}