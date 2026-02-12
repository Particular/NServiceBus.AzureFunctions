namespace IntegrationTest;

using NServiceBus;
using NServiceBus.AzureFunctions.AzureServiceBus;

public static class CommonEndpointConfig
{
    public static void Apply(EndpointConfiguration endpoint)
    {
        endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        endpoint.EnableInstallers();
        endpoint.UsePersistence<LearningPersistence>();
        endpoint.UseSerialization<SystemJsonSerializer>();
    }
}