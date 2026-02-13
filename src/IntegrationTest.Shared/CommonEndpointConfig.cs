namespace IntegrationTest.Shared;

public static class CommonEndpointConfig
{
    public static void Apply(EndpointConfiguration configuration)
    {
        configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        configuration.EnableInstallers();
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseSerialization<SystemJsonSerializer>();
    }
}