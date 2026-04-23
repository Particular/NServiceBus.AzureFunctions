namespace NServiceBus;

/// <summary>
/// Marks a class or method as the source of an NServiceBus endpoint hosted in Azure Functions.
/// The source generator produces the trigger function and endpoint registration code from types
/// marked with this attribute.
/// </summary>
/// <remarks>The target method, and any class containing it, must be declared <c>partial</c> so the source generator can emit the trigger body.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class NServiceBusFunctionAttribute : Attribute;