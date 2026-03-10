namespace NServiceBus;

using System.ComponentModel;
using Microsoft.Azure.Functions.Worker.Builder;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class NServiceBusFunctionsInfrastructure
{
    public static void Initialize(FunctionsApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
    }
}