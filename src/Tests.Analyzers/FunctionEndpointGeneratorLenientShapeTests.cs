namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Particular.AnalyzerTesting;
using NUnit.Framework;

[TestFixture]
public class FunctionEndpointGeneratorLenientShapeTests
{
    [Test]
    public void GeneratesEndpointWithExtraUnrecognizedParametersWhenShapeAllowsAdditionalParameters() =>
        SourceGeneratorTest.ForIncrementalGenerator<LenientNoMessageActionsGenerator>()
            .WithSource(SourceWithAdditionalParameter)
            .SuppressCompilationErrors()
            .Approve();

    const string SourceWithAdditionalParameter = """
        namespace Demo.Testing;

        [System.AttributeUsage(System.AttributeTargets.Parameter)]
        public class TestTriggerAttribute : System.Attribute
        {
            public TestTriggerAttribute(string queueName) { }
            public string? ConnSetting { get; set; }
            public bool AutoCompleteMessages { get; set; }
        }

        public class SomeCustomParameter { }

        public partial class Functions
        {
            [NServiceBusFunction]
            [Function("ProcessOrder")]
            public partial Task Run(
                [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                SomeCustomParameter extraParam,
                FunctionContext context,
                CancellationToken cancellationToken);

            public static void ConfigureProcessOrder(
                EndpointConfiguration endpointConfiguration)
            {
            }
        }
        """;
}
