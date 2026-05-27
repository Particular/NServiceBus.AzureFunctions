namespace NServiceBus;

/// <summary>
/// Marks a function method as the source of an NServiceBus endpoint hosted in Azure Functions.
/// The source generator produces the trigger body and endpoint registration code from methods
/// marked with this attribute.
/// </summary>
/// <remarks>The target method, and the class containing it, must be declared <c>partial</c> so the source generator can emit the trigger body.</remarks>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class NServiceBusFunctionAttribute : Attribute;