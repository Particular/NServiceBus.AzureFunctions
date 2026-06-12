namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using System.Collections.Generic;
using NUnit.Framework;

static class TestSources
{
    public static IEnumerable<TestCaseData> EndpointNameSanitizationCases
    {
        get
        {
            yield return new TestCaseData("my-endpoint", "Configuremyendpoint");
            yield return new TestCaseData("process.order", "Configureprocessorder");
            yield return new TestCaseData("my_endpoint", "Configuremyendpoint");
            yield return new TestCaseData("ProcessOrder", "configureprocessorder");
        }
    }
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
                IConfigurationManager iconfigurationmanager,
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
                IConfigurationManager iconfigurationmanager,
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
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointInGlobalNamespace = """
        using NServiceBus;
        using Microsoft.Extensions.DependencyInjection;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointWithAllAdditionalParameters = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.Hosting;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(
                EndpointConfiguration endpointConfiguration,
                IServiceCollection services,
                IConfigurationManager configuration,
                IHostEnvironment environment)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointWithNoAdditionalParameters = """
        using NServiceBus;
        namespace Demo;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration)
            {
            }
        }
        """;

    public const string MultipleSendOnlyEndpoints = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.DependencyInjection;
        using Microsoft.Extensions.Configuration;
        using Microsoft.Extensions.Hosting;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(
                EndpointConfiguration endpointConfiguration,
                IServiceCollection services)
            {
            }
        }

        public static class SenderEndpoint
        {
            [NServiceBusSendOnlyFunction("sender")]
            public static void ConfigureSender(
                EndpointConfiguration endpointConfiguration,
                IConfigurationManager configuration,
                IHostEnvironment environment)
            {
            }
        }
        """;

    public const string NoSendOnlyEndpoints = """
        namespace Demo;

        public static class SomeClass
        {
            public static void DoSomething() { }
        }
        """;

    public const string ValidSendOnlyEndpointWithConnection = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.DependencyInjection;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client", Connection = "MyCustomConnection")]
            public static void ConfigureClient(EndpointConfiguration endpointConfiguration, IServiceCollection services)
            {
            }
        }
        """;

    public const string ValidFunctionWithIConfigurationManager = """
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;
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
                Microsoft.Extensions.Configuration.IConfigurationManager configuration)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointWithIConfigurationManager = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.Hosting;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(
                EndpointConfiguration endpointConfiguration,
                Microsoft.Extensions.Configuration.IConfigurationManager configuration,
                IHostEnvironment environment)
            {
            }
        }
        """;

    public const string ValidFunctionWithIConfiguration = """
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;
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
                Microsoft.Extensions.Configuration.IConfiguration configuration)
            {
            }
        }
        """;

    public const string ValidFunctionWithIConfigurationBuilder = """
        using System.Threading;
        using System.Threading.Tasks;
        using Azure.Messaging.ServiceBus;
        using Microsoft.Azure.Functions.Worker;
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
                Microsoft.Extensions.Configuration.IConfigurationBuilder configuration)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointWithIConfiguration = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.Hosting;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(
                EndpointConfiguration endpointConfiguration,
                Microsoft.Extensions.Configuration.IConfiguration configuration,
                IHostEnvironment environment)
            {
            }
        }
        """;

    public const string ValidSendOnlyEndpointWithIConfigurationBuilder = """
        using NServiceBus;
        namespace Demo;

        using Microsoft.Extensions.Hosting;

        public static class ClientEndpoint
        {
            [NServiceBusSendOnlyFunction("client")]
            public static void ConfigureClient(
                EndpointConfiguration endpointConfiguration,
                Microsoft.Extensions.Configuration.IConfigurationBuilder configuration,
                IHostEnvironment environment)
            {
            }
        }
        """;
}