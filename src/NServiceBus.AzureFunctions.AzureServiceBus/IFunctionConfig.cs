namespace NServiceBus;

public interface IFunctionConfig
{
    void Configure(EndpointConfiguration endpoint);
}