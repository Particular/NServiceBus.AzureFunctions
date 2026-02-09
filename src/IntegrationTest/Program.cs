using IntegrationTest;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Logging;
using NServiceBus.MultiHosting;

var builder = FunctionsApplication.CreateBuilder(args);

// as early as possible
LogManager.UseFactory(MultiEndpointLoggerFactory.Instance);

builder.Services.AddHostedService<InitializeLogger>();

//TODO: What happens if this is reused for multiple endpoints?
var sharedTransport = new AzureServiceBusServerlessTransport(TopicTopology.Default);

//senderTransport.Routing.RouteToEndpoint(typeof(TriggerMessageHandler), "SenderEndpoint");
//
builder.AddNServiceBusFunction("SenderEndpoint",
    sharedTransport,
    endpoint =>
    {
        var routing2 = endpoint.UseTransport(sharedTransport);
        endpoint.SendOnly();
        endpoint.UseSerialization<SystemJsonSerializer>();
    },
    routing => routing.RouteToEndpoint(typeof(TriggerMessage), "ReceiverEndpoint"));

builder.AddNServiceBusFunctionAltB("SenderEndpoint",
    endpoint =>
    {
        var routing = endpoint.UseTransport(sharedTransport);
        routing.RouteToEndpoint(typeof(TriggerMessage), "ReceiverEndpoint");
        endpoint.SendOnly();
        endpoint.UseSerialization<SystemJsonSerializer>();
    });

builder.AddNServiceBusFunction("ReceiverEndpoint",
    sharedTransport,
    endpoint =>
    {
        endpoint.EnableInstallers();
        endpoint.UsePersistence<LearningPersistence>();
        endpoint.UseSerialization<SystemJsonSerializer>();

        endpoint.AddHandler<TriggerMessageHandler>();
        endpoint.AddHandler<SomeOtherMessageHandler>();
        endpoint.AddHandler<SomeEventMessageHandler>();
    });

builder.AddNServiceBusFunction("AnotherReceiverEndpoint",
    sharedTransport,
    endpoint =>
    {
        endpoint.EnableInstallers();
        endpoint.UsePersistence<LearningPersistence>();
        endpoint.UseSerialization<SystemJsonSerializer>();

        endpoint.AddHandler<SomeEventMessageHandler>();
    });

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
