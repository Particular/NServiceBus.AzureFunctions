namespace IntegrationTest.Billing;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

// Pattern for multiple endpoints in one class with separate configs
public partial class BillingFunctions
{
    [Function("BillingApi")]
    [NServiceBusFunction]
    public partial Task BillingApi(
        [ServiceBusTrigger("billing-api", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureBillingApi(EndpointConfiguration configuration)
    {
        CommonEndpointConfig.Apply(configuration);
        configuration.AddHandler<Handlers.ProcessPaymentHandler>();
    }

    [Function("BillingBackend")]
    [NServiceBusFunction]
    public partial Task BillingBackend(
        [ServiceBusTrigger("billing-backend", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureBillingBackend(EndpointConfiguration endpointConfiguration, IConfiguration configuration, IHostEnvironment environment)
    {
        CommonEndpointConfig.Apply(endpointConfiguration);

        if (environment.IsProduction())
        {
            endpointConfiguration.AuditProcessedMessagesTo(configuration["audit-queue"] ?? "audit");
        }
    }
}