namespace NServiceBus;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class NServiceBusFunctionAttribute : Attribute
{
    public NServiceBusFunctionAttribute() { }

    public NServiceBusFunctionAttribute(Type configurationType)
    {
        ConfigurationType = configurationType;
    }

    public Type? ConfigurationType { get; }
}