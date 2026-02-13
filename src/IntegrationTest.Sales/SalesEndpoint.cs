namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

[NServiceBusFunction(typeof(SalesEndpoint))]
public partial class SalesEndpoint : IEndpointConfiguration
{
    [Function("Sales")]
    public partial Task ProcessMessage(
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public void Configure(EndpointConfiguration endpoint)
    {
        CommonEndpointConfig.Apply(endpoint);
        endpoint.AddHandler<Handlers.AcceptOrderHandler>();
    }
}