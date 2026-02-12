namespace IntegrationTest.Business;

using NServiceBus;
using NServiceBus.AzureFunctions.AzureServiceBus;

public abstract class EndpointConfigBase
{
    public virtual void Configure(EndpointConfiguration endpoint)
    {
        endpoint.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        endpoint.EnableInstallers();
        endpoint.UsePersistence<LearningPersistence>();
        endpoint.UseSerialization<SystemJsonSerializer>();
    }
}