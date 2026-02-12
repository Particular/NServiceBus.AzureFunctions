namespace NServiceBus
{
    using Microsoft.Azure.Functions.Worker.Builder;

    public static class NServiceBusEndpoints
    {
        public static FunctionManifest ReceiverEndpoint = new("ReceiverEndpoint", "ReceiverEndpoint", "AzureWebJobsServiceBus");
        public static FunctionManifest AnotherReceiverEndpoint = new("AnotherReceiverEndpoint", "AnotherReceiverEndpoint", "AzureWebJobsServiceBus");
    }

    public record AnotherReceiverEndpoint2() : FunctionManifest("AnotherReceiverEndpoint2", "AnotherReceiverEndpoint2", "AzureWebJobsServiceBus");

    public record AnotherReceiverEndpoint3() : FunctionManifest("AnotherReceiverEndpoint3", "AnotherReceiverEndpoint3", "AzureWebJobsServiceBus");

    public static class GeneratedFunctionsExtensions
    {
        public static void AddAnotherEndpoint3NServiceBusFunction(
            this FunctionsApplicationBuilder builder,
            Action<EndpointConfiguration> configure) =>
            builder.AddNServiceBusFunction<AnotherReceiverEndpoint3>(configure);
    }

    // public static class NServiceBusFunctionsRegistration
    // {
    //     public static void UseNServiceBusFunctions(this FunctionsApplicationBuilder builder)
    //     {
    //         builder.Services.AddSingleton(new ExpectedNServiceBusFunction("SalesEndpoint"));
    //         builder.Services.AddSingleton(new ExpectedNServiceBusFunction("BillingEndpoint"));
    //
    //         builder.AddNServiceBusFunction<IntegrationTest.SalesEndpoint>("SalesEndpoint");
    //         builder.AddNServiceBusFunction<IntegrationTest.BillingEndpoint>("BillingEndpoint");
    //
    //         builder.Services.AddHostedService<FunctionConfigurationValidator>();
    //     }
    // }
    //
    // public partial class SalesEndpoint
    // {
    //     [Function("SalesEndpoint")]
    //     public Task ProcessMessage(
    //         [ServiceBusTrigger("SalesEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
    //         ServiceBusReceivedMessage message,
    //         FunctionContext functionContext,
    //         CancellationToken cancellationToken = default)
    //     {
    //         var processor = functionContext.InstanceServices
    //             .GetRequiredKeyedService<IMessageProcessor>("SalesEndpoint");
    //         return processor.Process(message, cancellationToken);
    //     }
    // }
}