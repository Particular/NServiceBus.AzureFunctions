namespace NServiceBus;

/// <summary>
/// Marks a static configuration method as the source of a send-only NServiceBus endpoint hosted in Azure Functions.
/// The source generator produces endpoint registration code from methods marked with this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NServiceBusSendOnlyEndpointAttribute(string endpointName) : Attribute
{
    /// <summary>
    /// Gets the logical endpoint name.
    /// </summary>
    public string EndpointName { get; } = endpointName;
}