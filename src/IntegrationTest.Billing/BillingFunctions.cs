namespace IntegrationTest.Billing;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Pattern for multiple endpoints in one class with separate configs
public partial class BillingFunctions
{
    [Function("BillingApi")]
    [NServiceBusFunction(typeof(ApiConfig))]
    public partial Task Api(
        [ServiceBusTrigger("billing-api", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public class ApiConfig : IEndpointConfiguration
    {
        public void Configure(EndpointConfiguration configuration)
        {
            CommonEndpointConfig.Apply(configuration);
            configuration.AddHandler<Handlers.ProcessPaymentHandler>();
        }
    }

    [Function("BillingBackend")]
    [NServiceBusFunction(typeof(BackendConfig))]
    public partial Task Backend(
        [ServiceBusTrigger("billing-backend", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public class BackendConfig : IEndpointConfiguration
    {
        public void Configure(EndpointConfiguration configuration)
        {
            CommonEndpointConfig.Apply(configuration);
            // different handlers for the backend queue
        }
    }
}