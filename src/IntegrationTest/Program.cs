using System.Text.Json;
using IntegrationTest;
using IntegrationTest.Shared;
using IntegrationTest.Shared.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);
builder.UseWhen<ExceptionTrackingMiddleware>(_ => true);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o =>
{
    o.IncludeScopes = true;
    o.JsonWriterOptions = new JsonWriterOptions { Indented = true };
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton<GlobalTestStorage>();
builder.Services.AddSingleton(new MyComponent("global"));

builder.AddNServiceBusFunctions();
builder.AddSendOnlyNServiceBusEndpoint("client", configuration =>
{
    configuration.RegisterComponents(services => services.AddSingleton(new MyComponent("client")));

    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { ConnectionName = "AzureWebJobsServiceBus" };

    var routing = configuration.UseTransport(transport);

    routing.RouteToEndpoint(typeof(SubmitOrder), "sales");
    configuration.UseSerialization<SystemJsonSerializer>();
});

var host = builder.Build();

await host.RunAsync();
