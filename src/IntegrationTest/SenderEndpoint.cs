namespace IntegrationTest;

using IntegrationTest.Shared;

[NServiceBusSendOnlyEndpoint]
class SenderEndpoint : IEndpointConfiguration
{
    public void Configure(EndpointConfiguration configuration)
    {
        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default)
        {
            ConnectionName = "AzureWebJobsServiceBus"
        };

        var routing = configuration.UseTransport(transport);

        routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
        configuration.UseSerialization<SystemJsonSerializer>();
    }
}