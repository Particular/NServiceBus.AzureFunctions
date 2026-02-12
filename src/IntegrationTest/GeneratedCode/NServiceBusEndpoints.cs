namespace NServiceBus;

public static class NServiceBusEndpoints
{
    public static FunctionManifest ReceiverEndpoint = new("ReceiverEndpoint", "ReceiverEndpoint", "AzureWebJobsServiceBus");
    public static FunctionManifest AnotherEndpoint = new("AnotherReceiverEndpoint", "AnotherReceiverEndpoint", "AzureWebJobsServiceBus");
}


public record AnotherEndpoint2() : FunctionManifest("AnotherReceiverEndpoint2", "AnotherReceiverEndpoint2", "AzureWebJobsServiceBus");