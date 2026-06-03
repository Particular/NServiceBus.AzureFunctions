namespace NServiceBus.AzureFunctions.Analyzers.Tests;

static class TestSources
{
    public const string OrdinaryFunctionOnly = """
        namespace Demo;

        public class Functions
        {
            [Function("ProcessOrder")]
            public Task Run(
                [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
                ServiceBusMessageActions messageActions,
                FunctionContext context,
                CancellationToken cancellationToken) => Task.CompletedTask;
        }
       """;

    public const string ValidFunction = """
        namespace Demo;

        public partial class Functions
        {
            [NServiceBusFunction]
            [Function("ProcessOrder")]
            public partial Task Run(
                [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
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

    public const string NoMessageActionsFunction = """
        namespace Demo.Testing;

        [System.AttributeUsage(System.AttributeTargets.Parameter)]
        public class TestTriggerAttribute : System.Attribute
        {
            public TestTriggerAttribute(string queueName) { }
            public string ConnSetting { get; set; }
            public bool AutoCompleteMessages { get; set; }
        }
        
        public class TestProcessor
        {
           public Task Process(string message, FunctionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        public static class TestFunctionManifestRegistration
        {
            public static void Register(global::Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder _, global::NServiceBus.FunctionManifest __) { }
        }

        public static class TestSendOnlyEndpointManifestRegistration
        {
            public static void Register(global::Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder _, global::NServiceBus.SendOnlyEndpointManifest __) { }
        }

        public partial class Functions
        {
            [NServiceBusFunction]
            [Function("ProcessOrder")]
            public partial Task Run(
                [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                FunctionContext context,
                CancellationToken cancellationToken);

            public static void ConfigureProcessOrder(
                EndpointConfiguration endpointConfiguration)
            {
            }
        }
        """;

    public const string ValidFunctionInGlobalNamespace = """
        public partial class Functions
        {
            [NServiceBusFunction]
            [Function("ProcessOrder")]
            public partial Task Run(
                [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
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

    public const string ValidSendOnlyEndpoint = """
        namespace Demo;

        using Microsoft.Extensions.DependencyInjection;

        file static class UsesGlobalTypes
        {
            public static void Use(
                FunctionContext functionContext,
                ServiceBusReceivedMessage message,
                IConfiguration configuration,
                IHostEnvironment environment)
            {
                CancellationToken cancellationToken = default;
                _ = cancellationToken;
                _ = Task.CompletedTask;
            }
        }

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyEndpoint("client")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
            {
            }
        }
        """;
}
