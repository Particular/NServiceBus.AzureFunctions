using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;

// Intentionally in the global namespace to make sure that the generator can handle it
#pragma warning disable CA1050
public partial class ShippingEndpoint
#pragma warning restore CA1050
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