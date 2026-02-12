namespace IntegrationTest.Business.Shipping;

[NServiceBusEndpoint]
public partial class ShippingEndpoint : EndpointConfigBase
{
    public override void Configure(EndpointConfiguration endpoint)
    {
        base.Configure(endpoint);
        endpoint.AddHandler<Handlers.ShippingHandler>();
    }
}
