using IntegrationTest;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.AzureFunctions;
using NServiceBus.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

// as early as possible
LogManager.UseFactory(FunctionsLoggerFactory.Instance);

builder.Services.AddHostedService<InitializeLogger>();

builder.AddNServiceBus("SenderEndpoint", endpoint =>
{
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    endpoint.SendOnly();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();
});

builder.AddNServiceBus("ReceiverEndpoint", endpoint =>
{
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    endpoint.EnableInstallers();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();

    endpoint.AddHandler<TriggerMessageHandler>();
    endpoint.AddHandler<SomeOtherMessageHandler>();
    endpoint.AddHandler<SomeEventMessageHandler>();
});

builder.AddNServiceBus("AnotherReceiverEndpoint", endpoint =>
{
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    endpoint.EnableInstallers();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();

    endpoint.AddHandler<SomeEventMessageHandler>();
});

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
