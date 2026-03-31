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

    [Test]
    public void GeneratesEndpointWithoutMessageActions() =>
        SourceGeneratorTest.ForIncrementalGenerator<NoMessageActionsGenerator>()
            .WithSource(TestSources.NoMessageActionsFunction)
            .SuppressCompilationErrors()
            .Approve();

    [Test]
    public void ReportsInvalidFunctionMethodWhenShapeContainsExtraUnrecognizedParameters()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
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
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("parameters must match required shape"));
    }

    #region Structural diagnostics (NSBFUNC001-006)

    [TestCase(FunctionClassMustBePartial, DiagnosticIds.ClassMustBePartial)]
    [TestCase(FunctionClassShouldNotImplementIHandleMessages, DiagnosticIds.ShouldNotImplementIHandleMessages)]
    [TestCase(FunctionMethodMustBePartial, DiagnosticIds.MethodMustBePartial)]
    [TestCase(MultipleConfigureMethods, DiagnosticIds.MultipleConfigureMethods)]
    [TestCase(MissingAutoComplete, DiagnosticIds.AutoCompleteEnabled)]
    [TestCase(AutoCompleteEnabled, DiagnosticIds.AutoCompleteEnabled)]
    public void ReportsStructuralDiagnostics(string source, string diagnosticId)
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

    const string MissingAutoComplete = """
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
       }
       """;

    const string AutoCompleteEnabled = """
       namespace Demo;

       public partial class Functions
       {
           [NServiceBusFunction]
           [Function("ProcessOrder")]
           public partial Task Run(
               [ServiceBusTrigger("sales-queue", AutoCompleteMessages = true)] ServiceBusReceivedMessage message,
               ServiceBusMessageActions messageActions,
               FunctionContext context,
               CancellationToken cancellationToken);

           public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
           {
           }
       }
       """;

    #endregion

    #region Invalid function method diagnostic (NSBFUNC007)

    [Test]
    public void ReportsInvalidFunctionMethodWhenTriggerParameterMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<FunctionEndpointGenerator>("""
           namespace Demo;

           public partial class Functions
           {
               [NServiceBusFunction]
               [Function("ProcessOrder")]
               public partial Task Run(
                   FunctionContext context,
                   CancellationToken cancellationToken);

               public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
               {
               }
           }
           """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("missing a parameter with a trigger attribute"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenMessageActionsRequiredButMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<FunctionEndpointGenerator>("""
           namespace Demo;

           public partial class Functions
           {
               [NServiceBusFunction]
               [Function("ProcessOrder")]
               public partial Task Run(
                   [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus")] ServiceBusReceivedMessage message,
                   FunctionContext context,
                   CancellationToken cancellationToken);

               public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
               {
               }
           }
           """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("missing MessageActions parameter"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenParameterOrderDoesNotMatchShape()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        FunctionContext context,
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                        CancellationToken cancellationToken);

                    public static void ConfigureProcessOrder(
                        EndpointConfiguration endpointConfiguration)
                    {
                    }
                }
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("parameters must match required shape"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenMultipleTriggerParametersExist()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string duplicate,
                        FunctionContext context,
                        CancellationToken cancellationToken);

                    public static void ConfigureProcessOrder(
                        EndpointConfiguration endpointConfiguration)
                    {
                    }
                }
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("must declare exactly one trigger parameter"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenFunctionContextMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                        CancellationToken cancellationToken);

                    public static void ConfigureProcessOrder(
                        EndpointConfiguration endpointConfiguration)
                    {
                    }
                }
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("missing FunctionContext parameter"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenCancellationTokenMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                        FunctionContext context);

                    public static void ConfigureProcessOrder(
                        EndpointConfiguration endpointConfiguration)
                    {
                    }
                }
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("missing CancellationToken parameter"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenConfigureMethodMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                        FunctionContext context,
                        CancellationToken cancellationToken);
                }
                """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("missing 'ConfigureProcessOrder' configuration method"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenTriggerEntityNameMissing()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource(
                triggerHasConstructor: false,
                classBody: """
                    public partial class Functions
                    {
                        [NServiceBusFunction]
                        [Function("ProcessOrder")]
                        public partial Task Run(
                            [TestTrigger(ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message,
                            FunctionContext context,
                            CancellationToken cancellationToken);

                        public static void ConfigureProcessOrder(
                            EndpointConfiguration endpointConfiguration)
                        {
                        }
                    }
                    """));

        Assert.That(diagnostic.GetMessage(), Does.Contain("trigger attribute does not specify a queue or entity name"));
    }

    [Test]
    public void ReportsAllProblemsInSingleDiagnostic()
    {
        var diagnostic = GetInvalidFunctionMethodDiagnostic<NoMessageActionsGenerator>(
            NoMessageActionsSource("""
                public partial class Functions
                {
                    [NServiceBusFunction]
                    [Function("ProcessOrder")]
                    public partial Task Run(
                        [TestTrigger("sales-queue", ConnSetting = "StorageConn", AutoCompleteMessages = false)] string message);
                }
                """));

        var message = diagnostic.GetMessage();
        Assert.That(message, Does.Contain("missing FunctionContext parameter"));
        Assert.That(message, Does.Contain("missing CancellationToken parameter"));
        Assert.That(message, Does.Contain("missing 'ConfigureProcessOrder' configuration method"));
    }

    [Test]
    public void DoesNotReportMissingMessageActionsWhenTransportDoesNotRequireThem()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<NoMessageActionsGenerator>()
            .WithSource(TestSources.NoMessageActionsFunction)
            .SuppressCompilationErrors()
            .Run();

        var diagnostics = result.GetGeneratorDiagnostics();
        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == "NSBFUNC007"));
    }

    #endregion

    #region Helpers

    static Diagnostic GetInvalidFunctionMethodDiagnostic<TGenerator>(string source) where TGenerator : IIncrementalGenerator, new()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<TGenerator>()
            .WithSource(source)
            .SuppressCompilationErrors()
            .SuppressDiagnosticErrors()
            .Run();

        var diagnostics = result.GetGeneratorDiagnostics();
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == "NSBFUNC007"),
            "Expected NSBFUNC007 diagnostic to be reported");

        return diagnostics.First(d => d.Id == "NSBFUNC007");
    }

    static string NoMessageActionsSource(string classBody, bool triggerHasConstructor = true)
    {
        var constructor = triggerHasConstructor
            ? "public TestTriggerAttribute(string queueName) { }"
            : "";

        return $$"""
            namespace Demo.Testing;

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class TestTriggerAttribute : System.Attribute
            {
                {{constructor}}
                public string? ConnSetting { get; set; }
                public bool AutoCompleteMessages { get; set; }
            }

            {{classBody}}
            """;
    }

    #endregion
}