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

builder.AddNServiceBusFunction("SenderEndpoint",
    new AzureServiceBusServerlessTransport(TopicTopology.Default),
    endpoint =>
{
    endpoint.SendOnly();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();
});

builder.AddNServiceBusFunction("ReceiverEndpoint",
    new AzureServiceBusServerlessTransport(TopicTopology.Default),
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
    new AzureServiceBusServerlessTransport(TopicTopology.Default),
    endpoint =>
{
    endpoint.EnableInstallers();
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();

    endpoint.AddHandler<SomeEventMessageHandler>();
});

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
