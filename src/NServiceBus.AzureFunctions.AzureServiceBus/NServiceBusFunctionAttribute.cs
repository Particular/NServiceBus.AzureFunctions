namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class NServiceBusFunctionAttribute : Attribute;