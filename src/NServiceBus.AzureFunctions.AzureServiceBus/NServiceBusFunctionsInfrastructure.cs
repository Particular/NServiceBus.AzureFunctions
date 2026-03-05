namespace NServiceBus;

using System.ComponentModel;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class NServiceBusFunctionsInfrastructure
{
    public static void Initialize(FunctionsApplicationBuilder builder)
    {
        builder.Services.AddAzureClientsCore();
    }
}