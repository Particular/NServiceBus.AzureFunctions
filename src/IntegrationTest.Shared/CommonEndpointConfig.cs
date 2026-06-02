namespace IntegrationTest.Shared;

using Infrastructure;

public static class CommonEndpointConfig
{
    public static void Apply(EndpointConfiguration endpointConfiguration)
    {
        endpointConfiguration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UsePersistence<LearningPersistence>();
        endpointConfiguration.UseSerialization<SystemJsonSerializer>();

        endpointConfiguration.EnableFeature<TestStorageFeature>();
    }
}