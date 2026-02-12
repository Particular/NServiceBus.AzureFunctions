namespace NServiceBus;

using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NServiceBusEndpointAttribute(string? endpointName = null) : Attribute
{
    /// <summary>
    /// The endpoint name. If null, the class name is used by convention.
    /// </summary>
    public string? EndpointName { get; } = endpointName;
}
