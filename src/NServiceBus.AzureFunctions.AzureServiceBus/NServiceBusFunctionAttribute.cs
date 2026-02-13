namespace NServiceBus;

public class NServiceBusFunctionAttribute(Type configurationType) : Attribute
{
    public Type ConfigurationType { get; } = configurationType;
}