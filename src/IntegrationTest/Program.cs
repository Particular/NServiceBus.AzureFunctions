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

// Send-only using a separate API since they are not "functions"
builder.AddSendOnlyNServiceBusEndpoint("SenderEndpoint", endpoint =>
{
    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default)
    {
        //send only endpoints might need to set the connection name
        ConnectionName = "AzureWebJobsServiceBus"
    };

    var routing = endpoint.UseTransport(transport);

    routing.RouteToEndpoint(typeof(TriggerMessage), "ReceiverEndpoint");
    endpoint.UseSerialization<SystemJsonSerializer>();
});


//NOTE: forgetting to register a function leads to "Exception: Unable to resolve service for type 'NServiceBus.AzureFunctions.AzureServiceBus.IMessageProcessor' while attempting to activate 'IntegrationTest.AnotherReceiverEndpoint3Function'."

//option 1: No source gen on the configuration side, user needs to use correct name, queue and connection name as the function definition
builder.AddNServiceBusFunction("ReceiverEndpoint", endpoint =>
{
    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default)
    {
        //this needs to match
        ConnectionName = "AzureWebJobsServiceBus"
    };

    // if they differ this needs to be done
    endpoint.OverrideLocalAddress("ReceiverEndpoint");
    endpoint.UseTransport(transport);
    endpoint.EnableInstallers();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();

    endpoint.AddHandler<TriggerMessageHandler>();
    endpoint.AddHandler<SomeOtherMessageHandler>();
    endpoint.AddHandler<SomeEventMessageHandler>();
});

//option 2: Pass in a manifest that we have source genned
builder.AddNServiceBusFunction(NServiceBusEndpoints.AnotherReceiverEndpoint, configuration =>
{
    configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    configuration.EnableInstallers();
    configuration.UsePersistence<LearningPersistence>();
    configuration.UseSerialization<SystemJsonSerializer>();

    configuration.AddHandler<SomeEventMessageHandler>();
});

//option 3: Use a type that we have source genned
builder.AddNServiceBusFunction<AnotherReceiverEndpoint2>(configuration =>
{
    configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    configuration.EnableInstallers();
    configuration.UsePersistence<LearningPersistence>();
    configuration.UseSerialization<SystemJsonSerializer>();

    configuration.AddHandler<SomeEventMessageHandler>();
});

//option 4: Use source genned method
builder.AddAnotherEndpoint3NServiceBusFunction(configuration =>
{
    configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    configuration.EnableInstallers();
    configuration.UsePersistence<LearningPersistence>();
    configuration.UseSerialization<SystemJsonSerializer>();

    configuration.AddHandler<SomeEventMessageHandler>();
});

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
