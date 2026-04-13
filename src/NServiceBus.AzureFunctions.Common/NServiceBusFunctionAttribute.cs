namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public sealed class NServiceBusFunctionAttribute : Attribute;