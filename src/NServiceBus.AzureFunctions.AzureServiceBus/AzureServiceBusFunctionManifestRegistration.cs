namespace NServiceBus.AzureFunctions.AzureServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

public static class AzureServiceBusFunctionManifestRegistration
{
    public static void Register(FunctionsApplicationBuilder builder, FunctionManifest functionManifest) =>
        FunctionsHostApplicationBuilderExtensions.AddNServiceBusFunction(builder, functionManifest);
}
