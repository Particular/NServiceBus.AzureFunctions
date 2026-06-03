namespace IntegrationTestApp;

using IntegrationTest.Shared;
using Microsoft.Extensions.DependencyInjection;

public static class ClientEndpoint
{
    [NServiceBusSendOnlyEndpoint("client", Connection = "AzureWebJobsServiceBus")]
    public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
    {
        services.AddSingleton(new MyComponent("client"));

        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default);

        var routing = endpointConfiguration.UseTransport(transport);

        routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();
    }
}