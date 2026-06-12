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
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [TestCase("my-endpoint", "Configuremyendpoint")]
    [TestCase("process.order", "Configureprocessorder")]
    [TestCase("my_endpoint", "Configuremyendpoint")]
    [TestCase("ProcessOrder", "configureprocessorder")]
    [TestCase("my-endpoint", "ConfigureMy_Endpoint")]
    [TestCase("my-endpoint", "Configuremy_endpoint")]
    [TestCase("ProcessOrder", "CONFIGUREprocessORDER")]
    [TestCase("ProcessOrder", "configure_process_order")]
    [TestCase("Lösung-Endpoint", "ConfigureLösungEndpoint")]
    public void FlexibleConfigureMethodNameMatches(string endpointName, string configureMethodName)
    {
        var source = $$"""
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
            [Function("{{endpointName}}")]
            public partial Task Run(
                [ServiceBusTrigger("sales-queue", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
                ServiceBusMessageActions messageActions,
                FunctionContext context,
                CancellationToken cancellationToken);

            public static void {{configureMethodName}}(EndpointConfiguration endpointConfiguration)
            {
            }
        }
        """;

        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(source)
            .Run();
    }

    [Test]
    public void GeneratesFunctionEndpointInGlobalNamespace() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunctionInGlobalNamespace)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesFunctionEndpointWithIConfigurationManagerParameter() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunctionWithIConfigurationManager)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesFunctionEndpointWithIConfigurationParameter() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunctionWithIConfiguration)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesFunctionEndpointWithIConfigurationBuilderParameter() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.ValidFunctionWithIConfigurationBuilder)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesNoRegistrationsForOrdinaryFunctionsOnly() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource(TestSources.OrdinaryFunctionOnly)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesEndpointWithoutMessageActions() =>
        SourceGeneratorTest.ForIncrementalGenerator<NoMessageActionsGenerator>()
            .WithSource(TestSources.NoMessageActionsFunction)
            .Run()
            .Approve()
            .AssertRunsAreEqual();

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

        var diagnostics = result.GeneratorDiagnostics;
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == diagnosticId));
    }

    const string FunctionClassMustBePartial = """
          using System.Threading;
          using System.Threading.Tasks;
          using Azure.Messaging.ServiceBus;
          using Microsoft.Azure.Functions.Worker;
          using Microsoft.Extensions.Configuration;
          using Microsoft.Extensions.Hosting;
          using NServiceBus;

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
                  IConfigurationManager iconfigurationmanager,
                  IHostEnvironment ihostenvironment)
              {
              }
          }
          """;

    const string FunctionMethodMustBePartial = """
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
                  IConfigurationManager iconfigurationmanager,
                  IHostEnvironment ihostenvironment)
              {
              }
          }
          """;

    const string FunctionClassShouldNotImplementIHandleMessages = """
         using System.Threading;
         using System.Threading.Tasks;
         using Azure.Messaging.ServiceBus;
         using Microsoft.Azure.Functions.Worker;
         using Microsoft.Extensions.Configuration;
         using Microsoft.Extensions.Hosting;
         using NServiceBus;

         namespace Demo;

         public partial class Functions : IHandleMessages<ServiceBusReceivedMessage>
         {
             [NServiceBusFunction]
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
                 IConfigurationManager iconfigurationmanager,
                 IHostEnvironment ihostenvironment)
             {
             }
         }
         """;

    [Test]
    public void ReportsIHandleMessagesWarningOnlyOnceForMultipleAttributedMethods()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
           .WithSource("""
                using System.Threading;
                using System.Threading.Tasks;
                using Azure.Messaging.ServiceBus;
                using Microsoft.Azure.Functions.Worker;
                using NServiceBus;

                namespace Demo;

                public partial class Functions : IHandleMessages<ServiceBusReceivedMessage>
                {
                   [NServiceBusFunction]
                   [Function("ProcessOrder")]
                   public partial Task Run(
                       [ServiceBusTrigger("sales-queue", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
                       ServiceBusMessageActions messageActions,
                       FunctionContext context,
                       CancellationToken cancellationToken);

                   [NServiceBusFunction]
                   [Function("ProcessOrder2")]
                   public partial Task Run2(
                       [ServiceBusTrigger("sales-queue-2", Connection = "AzureServiceBus", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
                       ServiceBusMessageActions messageActions,
                       FunctionContext context,
                       CancellationToken cancellationToken);

                   public Task Handle(ServiceBusReceivedMessage message, IMessageHandlerContext context)
                   {
                       return Task.CompletedTask;
                   }

                   public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
                   {
                   }

                   public static void ConfigureProcessOrder2(EndpointConfiguration endpointConfiguration)
                   {
                   }
                }
                """)
            .SuppressDiagnosticErrors()
            .Run();

        var diagnosticCount = 0;
        foreach (var diagnostic in result.GeneratorDiagnostics)
        {
            if (diagnostic.Id == DiagnosticIds.ShouldNotImplementIHandleMessages)
            {
                diagnosticCount++;
            }
        }

        Assert.That(diagnosticCount, Is.EqualTo(1));
    }

    const string MultipleConfigureMethods = """
       using System.Threading;
       using System.Threading.Tasks;
       using Azure.Messaging.ServiceBus;
       using Microsoft.Azure.Functions.Worker;
       using Microsoft.Extensions.Configuration;
       using NServiceBus;

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
               IConfigurationManager iconfigurationmanager)
           {
           }
       }
       """;

    const string MissingAutoComplete = """
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
           using System.Threading;
           using System.Threading.Tasks;
           using Microsoft.Azure.Functions.Worker;
           using NServiceBus;

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

        Assert.That(diagnostic.GetMessage(), Does.Contain("trigger attribute does not specify an address or entity name"));
    }

    [Test]
    public void ReportsInvalidFunctionMethodWhenServiceBusTriggerUsesTopicSubscriptionConstructor()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithSource("""
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
                    [ServiceBusTrigger("sales-topic", "sales-subscription", Connection = "AzureServiceBus", AutoCompleteMessages = true)] ServiceBusReceivedMessage message,
                    ServiceBusMessageActions messageActions,
                    FunctionContext context,
                    CancellationToken cancellationToken);

               public static void ConfigureProcessOrder(EndpointConfiguration endpointConfiguration)
               {
               }
            }
           """)
            .SuppressCompilationErrors()
            .SuppressDiagnosticErrors()
            .Run();

        var diagnostics = result.GeneratorDiagnostics;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.InvalidFunctionMethod));
            Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == DiagnosticIds.AutoCompleteEnabled));
        }
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
            .Run();

        var diagnostics = result.GeneratorDiagnostics;
        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == DiagnosticIds.InvalidFunctionMethod));
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

        var diagnostics = result.GeneratorDiagnostics;
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.InvalidFunctionMethod),
            $"Expected {DiagnosticIds.InvalidFunctionMethod} diagnostic to be reported");

        return diagnostics.First(d => d.Id == DiagnosticIds.InvalidFunctionMethod);
    }

    static string NoMessageActionsSource(string classBody, bool triggerHasConstructor = true)
    {
        var constructor = triggerHasConstructor
            ? "public TestTriggerAttribute(string queueName) { }"
            : "";

        return $$"""
            using System.Threading;
            using System.Threading.Tasks;
            using Microsoft.Azure.Functions.Worker;
            using NServiceBus;

            #nullable enable

            namespace Demo.Testing;

            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public class TestTriggerAttribute : System.Attribute
            {
                {{constructor}}
                public string? ConnSetting { get; set; }
                public bool AutoCompleteMessages { get; set; }
            }

            public static class TestFunctionManifestRegistration
            {
                public static void Register(global::Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder _, global::NServiceBus.FunctionManifest __) { }
            }

            public class TestProcessor
            {
                public Task Process(string message, FunctionContext context, CancellationToken cancellationToken) => Task.CompletedTask;
            }

            {{classBody}}
            """;
    }

    #endregion
}
