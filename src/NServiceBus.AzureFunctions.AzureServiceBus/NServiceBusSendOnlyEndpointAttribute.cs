namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NServiceBusSendOnlyEndpointAttribute : Attribute
{
    public NServiceBusSendOnlyEndpointAttribute() { }

    public NServiceBusSendOnlyEndpointAttribute(string endpointName)
    {
        EndpointName = endpointName;
    }

    public string? EndpointName { get; }
}