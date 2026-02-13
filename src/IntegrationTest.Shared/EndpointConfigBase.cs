namespace IntegrationTest.Shared;

public abstract class EndpointConfigBase : IEndpointConfiguration
{
    public virtual void Configure(EndpointConfiguration configuration)
    {
        configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        configuration.EnableInstallers();
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseSerialization<SystemJsonSerializer>();
    }
}