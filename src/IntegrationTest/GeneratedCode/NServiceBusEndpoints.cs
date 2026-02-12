namespace NServiceBus;

public static class NServiceBusEndpoints
{
    public static FunctionManifest ReceiverEndpoint = new("ReceiverEndpoint", "ReceiverEndpoint", "AzureWebJobsServiceBus");
    public static FunctionManifest AnotherEndpoint = new("AnotherReceiverEndpoint", "AnotherReceiverEndpoint", "AzureWebJobsServiceBus");
}