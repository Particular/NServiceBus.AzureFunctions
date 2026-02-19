namespace IntegrationTest.Shared;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Configuration.AdvancedExtensibility;

public static class CommonEndpointConfig
{
    public static void Apply(EndpointConfiguration configuration)
    {
        configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        configuration.EnableInstallers();
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseSerialization<SystemJsonSerializer>();

        configuration.Services.AddSingleton(new MyComponent{ Endpoint = configuration.GetSettings().Get<string>("NServiceBus.Routing.EndpointName")});

        if (configuration.Environment.EnvironmentName == Environments.Production)
        {
            configuration.AuditProcessedMessagesTo(configuration.Configuration["acme-audit-queue"] ?? "audit");
        }
    }
}