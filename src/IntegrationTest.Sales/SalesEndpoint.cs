namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Cleanest pattern for single-function endpoints
[NServiceBusFunction]
public partial class SalesEndpoint : EndpointConfigBase
{
    [Function("Sales")]
    public partial Task ProcessMessage(
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public override void Configure(EndpointConfiguration configuration)
    {
        base.Configure(configuration);
        configuration.AddHandler<Handlers.AcceptOrderHandler>();
    }
}