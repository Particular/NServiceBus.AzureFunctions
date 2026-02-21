using System.Text.Json;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.JsonWriterOptions = new JsonWriterOptions { Indented = true };
});
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.AddNServiceBusFunctions();
builder.AddSendOnlyNServiceBusEndpoint("client", configuration =>
{
    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { ConnectionName = "AzureWebJobsServiceBus" };

    var routing = configuration.UseTransport(transport);

    routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
    configuration.UseSerialization<SystemJsonSerializer>();
});

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
