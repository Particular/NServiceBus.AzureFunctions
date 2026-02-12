namespace IntegrationTest.Business.Sales;

[NServiceBusEndpoint]
public partial class SalesEndpoint : EndpointConfigBase
{
    public override void Configure(EndpointConfiguration endpoint)
    {
        base.Configure(endpoint);
        endpoint.AddHandler<Handlers.AcceptOrderHandler>();
    }
}