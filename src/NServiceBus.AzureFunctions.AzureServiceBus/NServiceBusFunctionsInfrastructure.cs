namespace NServiceBus;

using System.ComponentModel;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class NServiceBusFunctionsInfrastructure
{
    public static void Initialize(FunctionsApplicationBuilder builder)
    {
        builder.Services.AddAzureClientsCore();
    }
}