using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddNServiceBusFunctions();

// Send-only using a separate API since they are not "functions"
builder.AddSendOnlyNServiceBusEndpoint("SenderEndpoint", endpoint =>
{
    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default)
    {
        //send only endpoints might need to set the connection name
        ConnectionName = "AzureWebJobsServiceBus"
    };

    var routing = endpoint.UseTransport(transport);

    routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
    endpoint.UseSerialization<SystemJsonSerializer>();
});

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
