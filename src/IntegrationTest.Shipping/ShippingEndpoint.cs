using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Intentionally in the global namespace to make sure that the generator can handle it
public partial class ShippingEndpoint
{
    [NServiceBusFunction]
    [Function(nameof(Shipping))]
    public partial Task Shipping(
        [ServiceBusTrigger("shipping", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureShipping(EndpointConfiguration endpointConfiguration)
    {
        CommonEndpointConfig.Apply(endpointConfiguration);
        endpointConfiguration.AddHandler<ShipOrderHandler>();
    }
}