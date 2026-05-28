namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using System.Threading.Tasks;
using NServiceBus.AzureFunctions.Analyzer;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[TestFixture]
public class ConfigurationAnalyzerTests : AnalyzerTestFixture<ConfigurationAnalyzer>
{
    static readonly TestCaseData[] UnsupportedEndpointConfigurationCallCases =
    [
        new("PurgeOnStartup(true)", DiagnosticIds.PurgeOnStartupNotAllowed),
        new("LimitMessageProcessingConcurrencyTo(5)", DiagnosticIds.LimitMessageProcessingToNotAllowed),
        new("DefineCriticalErrorAction((errorContext, cancellationToken) => Task.CompletedTask)", DiagnosticIds.DefineCriticalErrorActionNotAllowed),
        new("SetDiagnosticsPath(\"diagnostics\")", DiagnosticIds.SetDiagnosticsPathNotAllowed),
        new("MakeInstanceUniquelyAddressable(\"instance\")", DiagnosticIds.MakeInstanceUniquelyAddressableNotAllowed),
        new("UniquelyIdentifyRunningInstance()", DiagnosticIds.MakeInstanceUniquelyAddressableNotAllowed),
        new("OverrideLocalAddress(\"sales\")", DiagnosticIds.OverrideLocalAddressNotAllowed),
        new("UseTransport(new LearningTransport())", DiagnosticIds.UseTransportRequiresAzureServiceBusServerlessTransport)
    ];

    static readonly TestCaseData[] UnsupportedSendAndReplyOptionCases =
    [
        new("SendOptions", "RouteReplyToThisInstance", DiagnosticIds.RouteReplyToThisInstanceNotAllowed),
        new("SendOptions", "RouteToThisInstance", DiagnosticIds.RouteToThisInstanceNotAllowed),
        new("ReplyOptions", "RouteReplyToThisInstance", DiagnosticIds.RouteReplyToThisInstanceNotAllowed)
    ];

    static readonly TestCaseData[] UnsupportedUnrelatedOptionMethodCases =
    [
        new("RouteReplyToThisInstance", DiagnosticIds.RouteReplyToThisInstanceNotAllowed),
        new("RouteToThisInstance", DiagnosticIds.RouteToThisInstanceNotAllowed)
    ];

    [TestCaseSource(nameof(UnsupportedEndpointConfigurationCallCases))]
    public Task ReportsDiagnosticForUnsupportedEndpointConfigurationCalls(string configuration, string diagnosticId)
    {
        var source = $$"""
            namespace Demo;

            public class Functions
            {
                [NServiceBusFunction]
                [Function("ProcessOrder")]
                public Task Run(
                    [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
                    ServiceBusMessageActions messageActions,
                    FunctionContext context,
                    CancellationToken cancellationToken) => Task.CompletedTask;

                public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
                {
                    [|endpointConfiguration.{{configuration}}|];

                    var configurationCopy = endpointConfiguration;
                    [|configurationCopy.{{configuration}}|];
                }
            }
            """;

        return Assert(source, diagnosticId);
    }

    [TestCaseSource(nameof(UnsupportedEndpointConfigurationCallCases))]
    public Task ReportsDiagnosticForUnsupportedEndpointConfigurationCallsInHelperMethods(string configuration, string diagnosticId)
    {
        var source = $$"""
            namespace Demo;

            public static class CommonEndpointConfig
            {
                public static void Apply(EndpointConfiguration endpointConfiguration)
                {
                    [|endpointConfiguration.{{configuration}}|];
                }
            }
            """;

        return Assert(source, diagnosticId);
    }

    [TestCaseSource(nameof(UnsupportedEndpointConfigurationCallCases))]
    public Task DoesNotReportEndpointConfigurationCallsInMethodsWithoutSupportedSignature(string configuration, string diagnosticId)
    {
        var source = $$"""
            namespace Demo;

            public static class CommonEndpointConfig
            {
                public static void Apply(string name, EndpointConfiguration endpointConfiguration)
                {
                    endpointConfiguration.{{configuration}};
                }
            }
            """;

        return Assert(source, diagnosticId);
    }

    [TestCaseSource(nameof(UnsupportedSendAndReplyOptionCases))]
    public Task ReportsDiagnosticForUnsupportedSendAndReplyOptions(string optionsType, string method, string diagnosticId)
    {
        var source = $$"""
            namespace Demo;

            public class Functions
            {
                public void Send({{optionsType}} options)
                {
                    [|options.{{method}}()|];
                }
            }
            """;

        return Assert(source, diagnosticId);
    }

    [Test]
    public Task DoesNotReportUseTransportWithAzureServiceBusServerlessTransport()
    {
        var source = """
            using Microsoft.Azure.Functions.Worker.Builder;
            using NServiceBus.AzureFunctions.AzureServiceBus;
            using NServiceBus.Transport.AzureServiceBus;
            namespace Demo;

            public static class Program
            {
                public static void Configure(FunctionsApplicationBuilder builder)
                {
                    builder.AddSendOnlyNServiceBusEndpoint("client", (endpointConfiguration, services) =>
                    {
                        endpointConfiguration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
                    });
                }
            }
            """;

        return Assert(source);
    }

    [Test]
    public Task DoesNotReportUseTransportWithAzureServiceBusServerlessTransportVariable()
    {
        var source = """
            using NServiceBus.AzureFunctions.AzureServiceBus;
            using NServiceBus.Transport.AzureServiceBus;
            namespace Demo;

            public static class CommonEndpointConfig
            {
                public static void Apply(EndpointConfiguration endpointConfiguration)
                {
                    var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default);
                    endpointConfiguration.UseTransport(transport);
                }
            }
            """;

        return Assert(source);
    }

    [TestCaseSource(nameof(UnsupportedEndpointConfigurationCallCases))]
    public Task ReportsDiagnosticForUnsupportedEndpointConfigurationCallsInSendOnlyCallbacks(string configuration, string diagnosticId)
    {
        var source = $$"""
            using Microsoft.Azure.Functions.Worker.Builder;
            namespace Demo;

            public static class Program
            {
                public static void Configure(FunctionsApplicationBuilder builder)
                {
                    builder.AddSendOnlyNServiceBusEndpoint("client", (endpointConfiguration, services) =>
                    {
                        [|endpointConfiguration.{{configuration}}|];
                    });
                }
            }
            """;

        return Assert(source, diagnosticId);
    }

    [TestCaseSource(nameof(UnsupportedUnrelatedOptionMethodCases))]
    public Task DoesNotReportUnrelatedOptionTypes(string method, string diagnosticId)
    {
        var source = $$"""
            namespace Demo;

            public class SomeOtherOptions
            {
                public void RouteReplyToThisInstance()
                {
                }

                public void RouteToThisInstance()
                {
                }
            }

            public class Functions
            {
                public void Send(SomeOtherOptions options)
                {
                    options.{{method}}();
                }
            }
            """;

        return Assert(source, diagnosticId);
    }
}
