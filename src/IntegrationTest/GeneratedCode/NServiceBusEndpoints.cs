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

    public static class FunctionsHostApplicationBuilderExtensions
    {
        public static void AddAnotherEndpoint3NServiceBusFunction(
            this FunctionsApplicationBuilder builder,
            Action<EndpointConfiguration> configure) =>
            builder.AddNServiceBusFunction<AnotherReceiverEndpoint3>(configure);
    }

    // --- Fake source gen for class-based endpoint model ---
    // A real source generator would discover all [NServiceBusEndpoint] classes,
    // generate the function stubs below, and emit UseNServiceBusFunctions.

    public static class NServiceBusFunctionsRegistration
    {
        public static void UseNServiceBusFunctions(this FunctionsApplicationBuilder builder)
        {
            builder.AddNServiceBusFunction("SalesEndpoint", IntegrationTest.SalesEndpoint.Configure);
            builder.AddNServiceBusFunction("BillingEndpoint", IntegrationTest.BillingEndpoint.Configure);
        }
    }
}

// --- Generated function stubs (one partial per [NServiceBusEndpoint] class) ---
// The user never writes these - source gen creates them from the attribute.

namespace IntegrationTest
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus.AzureFunctions.AzureServiceBus;

    public partial class SalesEndpoint
    {
        [Function("SalesEndpoint")]
        public Task ProcessMessage(
            [ServiceBusTrigger("SalesEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
            ServiceBusReceivedMessage message,
            FunctionContext functionContext,
            CancellationToken cancellationToken = default)
        {
            var processor = functionContext.InstanceServices
                .GetRequiredKeyedService<IMessageProcessor>("SalesEndpoint");
            return processor.Process(message, cancellationToken);
        }
    }

    public partial class BillingEndpoint
    {
        [Function("BillingEndpoint")]
        public Task ProcessMessage(
            [ServiceBusTrigger("BillingEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
            ServiceBusReceivedMessage message,
            FunctionContext functionContext,
            CancellationToken cancellationToken = default)
        {
            var processor = functionContext.InstanceServices
                .GetRequiredKeyedService<IMessageProcessor>("BillingEndpoint");
            return processor.Process(message, cancellationToken);
        }
    }
}