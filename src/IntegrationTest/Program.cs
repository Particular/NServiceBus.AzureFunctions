using IntegrationTest;
using IntegrationTest.Infrastructure;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Logging;
using NServiceBus.MultiHosting;

var builder = FunctionsApplication.CreateBuilder(args);

// as early as possible
LogManager.UseFactory(MultiEndpointLoggerFactory.Instance);

builder.Services.AddHostedService<InitializeLogger>();
builder.Services.AddSingleton<GlobalTestStorage>();

void CommonEndpointSettings(EndpointConfiguration endpoint)
{
    endpoint.UsePersistence<LearningPersistence>();
    endpoint.UseSerialization<SystemJsonSerializer>();
    endpoint.EnableFeature<TestStorageFeature>();
    endpoint.EnableInstallers();
}

builder.AddNServiceBusFunction("SenderEndpoint", endpoint =>
{
    CommonEndpointSettings(endpoint);
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
    endpoint.SendOnly();
});

builder.AddNServiceBusFunction("ReceiverEndpoint", endpoint =>
{
    CommonEndpointSettings(endpoint);
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));

    endpoint.AddHandler<TriggerMessageHandler>();
    endpoint.AddHandler<SomeOtherMessageHandler>();
    endpoint.AddHandler<SomeEventMessageHandler>();
});

builder.AddNServiceBusFunction("AnotherReceiverEndpoint", endpoint =>
{
    CommonEndpointSettings(endpoint);
    endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default)
    {
        //ConnectionName = "AnotherServiceBusConnection"
    });

    endpoint.AddHandler<SomeEventMessageHandler>();
});

var host = builder.Build();

await host.RunAsync();
