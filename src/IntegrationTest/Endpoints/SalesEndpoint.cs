namespace IntegrationTest;

using NServiceBus;

[NServiceBusEndpoint]
public partial class SalesEndpoint
{
    public static void Configure(EndpointConfiguration endpoint)
    {
        CommonEndpointConfig.Apply(endpoint);

        endpoint.AddHandler<SomeEventMessageHandler>();
    }
}