namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using NServiceBus.AzureFunctions.Analyzer;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[TestFixture]
public class FunctionEndpointGeneratorTests
{
    [Test]
    public void GeneratesFunctionEndpoint() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunction)
            .SuppressCompilationErrors()
            .Approve();

    [TestCase(FunctionClassMustBePartial, "NSBFUNC001")]
    [TestCase(FunctionClassShouldNotImplementIHandleMessages, "NSBFUNC002")]
    [TestCase(FunctionMethodMustBePartial, "NSBFUNC003")]
    [TestCase(MultipleConfigureMethods, "NSBFUNC005")]
    public void ReportsGeneratorDiagnostics(string source, string diagnosticId)
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(source)
            .SuppressCompilationErrors()
            .SuppressDiagnosticErrors()
            .Run();

        var diagnostics = result.GetGeneratorDiagnostics();
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == diagnosticId));
    }

    const string FunctionClassMustBePartial = """
          namespace Demo;

          public class Functions
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

    const string FunctionMethodMustBePartial = """
          namespace Demo;

          public partial class Functions
          {
              [NServiceBusFunction]
              [Function("ProcessOrder")]
              public Task Run(
                  [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus")] ServiceBusReceivedMessage message,
                  ServiceBusMessageActions messageActions,
                  FunctionContext context,
                  CancellationToken cancellationToken)
              {
                  return Task.CompletedTask;
              }

              public static void ConfigureProcessOrder(
                  EndpointConfiguration endpointConfiguration,
                  IConfiguration iconfiguration,
                  IHostEnvironment ihostenvironment)
              {
              }
          }
          """;

    const string FunctionClassShouldNotImplementIHandleMessages = """
         namespace Demo;

         [NServiceBusFunction]
         public partial class Functions : IHandleMessages<ServiceBusReceivedMessage>
         {
             [Function("ProcessOrder")]
             public partial Task Run(
                 [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus")] ServiceBusReceivedMessage message,
                 ServiceBusMessageActions messageActions,
                 FunctionContext context,
                 CancellationToken cancellationToken);

             public Task Handle(ServiceBusReceivedMessage message, IMessageHandlerContext context)
             {
                 return Task.CompletedTask;
             }

             public static void ConfigureProcessOrder(
                 EndpointConfiguration endpointConfiguration,
                 IConfiguration iconfiguration,
                 IHostEnvironment ihostenvironment)
             {
             }
         }
         """;

    const string MultipleConfigureMethods = """
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

           public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
           {
           }

           public static void ConfigureProcessOrder(
               EndpointConfiguration endpointConfiguration,
               IConfiguration iconfiguration)
           {
           }
       }
       """;
}