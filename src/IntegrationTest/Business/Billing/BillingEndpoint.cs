namespace IntegrationTest.Business.Billing;

[NServiceBusEndpoint]
public partial class BillingEndpoint : EndpointConfigBase
{
    public override void Configure(EndpointConfiguration endpoint)
    {
        base.Configure(endpoint);
        endpoint.AddHandler<Handlers.ProcessPaymentHandler>();
    }
}