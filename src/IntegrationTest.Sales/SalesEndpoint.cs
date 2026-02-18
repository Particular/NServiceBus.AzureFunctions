namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Cleanest pattern for single-function endpoints
[NServiceBusFunction]
public partial class SalesEndpoint
{
    [Function("Sales")]
    public partial Task Sales(
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureSales(EndpointConfiguration configuration)
    {
        CommonEndpointConfig.Apply(configuration);
        configuration.AddHandler<Handlers.AcceptOrderHandler>();
    }
}