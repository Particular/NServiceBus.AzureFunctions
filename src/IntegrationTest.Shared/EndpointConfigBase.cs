namespace IntegrationTest.Shared;

public abstract class EndpointConfigBase : IEndpointConfiguration
{
    public virtual void Configure(EndpointConfiguration configuration) => CommonEndpointConfig.Apply(configuration);
}