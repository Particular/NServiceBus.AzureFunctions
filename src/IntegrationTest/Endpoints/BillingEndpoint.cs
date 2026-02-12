namespace IntegrationTest;

using NServiceBus;

[NServiceBusEndpoint]
public partial class BillingEndpoint
{
    public static void Configure(EndpointConfiguration endpoint)
    {
        CommonEndpointConfig.Apply(endpoint);

        endpoint.AddHandler<TriggerMessageHandler>();
        endpoint.AddHandler<SomeOtherMessageHandler>();
    }
}