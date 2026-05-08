using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Intentionally in the global namespace to make sure that the generator can handle it
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
        endpoint.AddHandler<ShipOrderHandler>();
    }
}