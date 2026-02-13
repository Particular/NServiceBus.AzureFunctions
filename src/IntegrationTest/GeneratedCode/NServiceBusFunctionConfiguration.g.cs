namespace NServiceBus;

using System;
using AzureFunctions.AzureServiceBus;
using Configuration.AdvancedExtensibility;
using IntegrationTest;
using Logging;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using MultiHosting;
using Settings;
using Transport;

public static class NServiceBusFunctionsHostApplicationBuilderExtensions
{
    public static void AddNServiceBusFunctions(
        this FunctionsApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        LogManager.UseFactory(MultiEndpointLoggerFactory.Instance);

        builder.Services.AddHostedService<InitializeLogger>();
        builder.Services.AddAzureClientsCore();

        foreach (var manifest in AllFunctions)
        {
            builder.AddNServiceBusFunction(manifest, manifest.EndpointConfiguration);   
        }
    }

    static List<FunctionManifest> AllFunctions = [new BillingApi(), new BillingBackend()];
    
    record BillingApi() : FunctionManifest("billing-api", "billing-api", "AzureWebJobsServiceBus", new BillingFunctions.ApiConfig());
    record BillingBackend() : FunctionManifest("billing-backend", "billing-backend", "AzureWebJobsServiceBus", new BillingFunctions.BackendConfig());
}