namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NServiceBusSendOnlyEndpointAttribute(string? endpointName = null) : Attribute
{
    public string? EndpointName { get; } = endpointName;
}