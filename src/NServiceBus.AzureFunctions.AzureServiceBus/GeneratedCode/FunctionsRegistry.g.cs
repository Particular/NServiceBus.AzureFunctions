namespace NServiceBus;

static partial class FunctionsRegistry
{
    static partial void AddGeneratedFunctions(List<FunctionManifest> entries)
    {
        entries.Add(new("ReceiverEndpoint", "ReceiverEndpoint", "AzureWebJobsServiceBus"));
        entries.Add(new("AnotherReceiverEndpoint", "AnotherReceiverEndpoint", "AzureWebJobsServiceBus"));
    }
}