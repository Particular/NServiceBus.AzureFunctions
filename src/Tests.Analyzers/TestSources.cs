namespace NServiceBus.AzureFunctions.Analyzers.Tests;

static class TestSources
{
    public const string OrdinaryFunctionOnly = """
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;

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
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.Hosting;
        using NServiceBus;

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
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Azure.Functions.Worker;
        using NServiceBus;

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
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.Hosting;
        using NServiceBus;

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
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.DependencyInjection;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyEndpoint("client")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
            {
            }
        }
        """;
}
