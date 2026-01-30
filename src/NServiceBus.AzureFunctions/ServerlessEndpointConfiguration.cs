namespace NServiceBus;

using NServiceBus.Persistence;
using NServiceBus.Serialization;

public class ServerlessEndpointConfiguration
{
    internal ServerlessEndpointConfiguration(string endpointName)
    {
        EndpointConfiguration = new EndpointConfiguration(endpointName);

        var assemblyScanner = EndpointConfiguration.AssemblyScanner();
        assemblyScanner.Disable = true;
    }

    public EndpointConfiguration EndpointConfiguration { get; }

    internal ServerlessTransport? Transport { get; private set; }

    public void UseTransport(ServerlessTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        Transport = transport;
        EndpointConfiguration.UseTransport(transport);
    }

    public void AddHandler<T>() where T : class, IHandleMessages
    {
        EndpointConfiguration.AddHandler<T>();
    }

    public SerializationExtensions<T> UseSerialization<T>() where T : SerializationDefinition, new()
    {
        return EndpointConfiguration.UseSerialization<T>();
    }

    public PersistenceExtensions<T> UsePersistence<T>() where T : PersistenceDefinition, IPersistenceDefinitionFactory<T>
    {
        return EndpointConfiguration.UsePersistence<T>();
    }

    public void EnableInstallers()
    {
        EndpointConfiguration.EnableInstallers();
    }

    public void SendOnly()
    {
        EndpointConfiguration.SendOnly();
    }
}