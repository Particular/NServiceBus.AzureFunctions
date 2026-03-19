namespace IntegrationTest.Shipping;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

[NServiceBusFunction]
public partial class ShippingEndpoint
{
    [Function(nameof(Shipping))]
    public partial Task Shipping(
        [ServiceBusTrigger("shipping", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureShipping(EndpointConfiguration endpoint)
    {
        CommonEndpointConfig.Apply(endpoint);
        endpoint.AddHandler<Handlers.ShipOrderHandler>();
    }
}