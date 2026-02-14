namespace IntegrationTest.Shipping;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

[NServiceBusFunction]
public partial class ShippingEndpoint : IEndpointConfiguration
{
    [Function("Shipping")]
    public partial Task ProcessMessage(
        [ServiceBusTrigger("shipping", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public void Configure(EndpointConfiguration endpoint)
    {
        CommonEndpointConfig.Apply(endpoint);
        endpoint.AddHandler<Handlers.ShipOrderHandler>();
    }
}