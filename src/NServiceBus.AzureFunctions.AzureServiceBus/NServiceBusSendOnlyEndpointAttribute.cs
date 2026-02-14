namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class NServiceBusSendOnlyEndpointAttribute(string? endpointName = null, Type? configurationType = null) : Attribute
{
    public string? EndpointName { get; } = endpointName;

    public Type? ConfigurationType { get; } = configurationType;
}