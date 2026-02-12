namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

public static class NServiceBusEndpoints
{
    public static FunctionManifest ReceiverEndpoint = new("ReceiverEndpoint", "ReceiverEndpoint", "AzureWebJobsServiceBus");
    public static FunctionManifest AnotherReceiverEndpoint = new("AnotherReceiverEndpoint", "AnotherReceiverEndpoint", "AzureWebJobsServiceBus");
}


public record AnotherReceiverEndpoint2() : FunctionManifest("AnotherReceiverEndpoint2", "AnotherReceiverEndpoint2", "AzureWebJobsServiceBus");

public record AnotherReceiverEndpoint3() : FunctionManifest("AnotherReceiverEndpoint3", "AnotherReceiverEndpoint3", "AzureWebJobsServiceBus");

public static class FunctionsHostApplicationBuilderExtensions
{
    public static void AddAnotherEndpoint3NServiceBusFunction(
        this FunctionsApplicationBuilder builder,
        Action<EndpointConfiguration> configure) =>
        builder.AddNServiceBusFunction<AnotherReceiverEndpoint3>(configure);
}