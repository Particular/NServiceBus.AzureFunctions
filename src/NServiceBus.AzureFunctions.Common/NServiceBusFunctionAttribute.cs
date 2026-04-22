namespace NServiceBus;

/// <summary>
/// Marks a class or method as an NServiceBus endpoint hosted in Azure Functions. The source
/// generator produces the trigger function and endpoint registration code from types marked with
/// this attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class NServiceBusFunctionAttribute : Attribute;