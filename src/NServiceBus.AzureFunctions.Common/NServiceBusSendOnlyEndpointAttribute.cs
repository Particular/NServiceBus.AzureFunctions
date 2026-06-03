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

    /// <summary>
    /// Gets or sets the name of the application setting or configuration section that contains the transport connection details.
    /// When not set, the trigger's default connection name is used.
    /// </summary>
    /// <remarks>
    /// This property holds the <em>name</em> of a configuration key, not the connection string value itself.
    /// At runtime, the transport looks up this key in the application configuration to resolve the actual connection details.
    /// </remarks>
    public string? Connection { get; set; }
}