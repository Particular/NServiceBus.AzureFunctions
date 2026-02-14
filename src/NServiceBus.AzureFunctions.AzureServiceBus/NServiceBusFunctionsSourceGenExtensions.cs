namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

public static class NServiceBusFunctionsSourceGenExtensions
{
    public static void AddNServiceBusFunctions(this FunctionsApplicationBuilder builder)
        => throw new NotImplementedException(
            "Source generators must be enabled to use AddNServiceBusFunctions.");
}