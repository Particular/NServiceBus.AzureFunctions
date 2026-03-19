namespace NServiceBus.AzureFunctions.Analyzers.Tests;

static class TestSources
{
    public const string ValidFunction = """
        namespace Demo;

        public partial class Functions
        {
            [NServiceBusFunction]
            [Function("ProcessOrder")]
            public partial Task Run(
                [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus")] ServiceBusReceivedMessage message,
                ServiceBusMessageActions messageActions,
                FunctionContext context,
                CancellationToken cancellationToken);

            public static void ConfigureProcessOrder(
                EndpointConfiguration endpointConfiguration,
                IConfiguration iconfiguration,
                IHostEnvironment ihostenvironment)
            {
            }
        }
        """;
}