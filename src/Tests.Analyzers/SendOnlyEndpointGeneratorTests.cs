namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Microsoft.CodeAnalysis;
using NServiceBus.AzureFunctions.Analyzer;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[TestFixture]
public class SendOnlyEndpointGeneratorTests
{
    [Test]
    public void GeneratesSendOnlyEndpointRegistration() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.ValidSendOnlyEndpoint, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesSendOnlyEndpointInGlobalNamespace() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.ValidSendOnlyEndpointInGlobalNamespace, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesSendOnlyEndpointWithAllAdditionalParameters() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.ValidSendOnlyEndpointWithAllAdditionalParameters, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesSendOnlyEndpointWithNoAdditionalParameters() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.ValidSendOnlyEndpointWithNoAdditionalParameters, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesNoRegistrationsWhenNoSendOnlyEndpoints() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.NoSendOnlyEndpoints, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void GeneratesMultipleSendOnlyEndpoints() =>
        SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.MultipleSendOnlyEndpoints, "SendOnly.cs")
            .Run()
            .Approve()
            .AssertRunsAreEqual();

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenMethodIsNotStatic()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource("""
                using NServiceBus;

                namespace Demo;

                public class ClientEndpoint
                {
                    [NServiceBusSendOnlyEndpoint("client")]
                    public void ConfigureClient(EndpointConfiguration endpointConfiguration)
                    {
                    }
                }
                """)
            .SuppressCompilationErrors()
            .SuppressDiagnosticErrors()
            .Run();

        Assert.That(result.GeneratorDiagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.InvalidSendOnlyEndpointMethod));
    }

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenMethodNameWrong()
    {
        var diagnostic = GetInvalidSendOnlyEndpointMethodDiagnostic("""
            using NServiceBus;

            namespace Demo;

            public static class ClientEndpoint
            {
                [NServiceBusSendOnlyEndpoint("client")]
                public static void WrongName(EndpointConfiguration endpointConfiguration)
                {
                }
            }
            """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("method name must be 'Configureclient'"));
    }

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenFirstParameterNotEndpointConfiguration()
    {
        var diagnostic = GetInvalidSendOnlyEndpointMethodDiagnostic("""
            using NServiceBus;

            namespace Demo;

            public static class ClientEndpoint
            {
                [NServiceBusSendOnlyEndpoint("client")]
                public static void ConfigureClient(string wrongParam)
                {
                }
            }
            """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("first parameter must be EndpointConfiguration"));
    }

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenNoParameters()
    {
        var diagnostic = GetInvalidSendOnlyEndpointMethodDiagnostic("""
            using NServiceBus;

            namespace Demo;

            public static class ClientEndpoint
            {
                [NServiceBusSendOnlyEndpoint("client")]
                public static void ConfigureClient()
                {
                }
            }
            """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("first parameter must be EndpointConfiguration"));
    }

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenInvalidAdditionalParameter()
    {
        var diagnostic = GetInvalidSendOnlyEndpointMethodDiagnostic("""
            using NServiceBus;

            namespace Demo;

            public static class ClientEndpoint
            {
                [NServiceBusSendOnlyEndpoint("client")]
                public static void ConfigureClient(EndpointConfiguration endpointConfiguration, string invalidParam)
                {
                }
            }
            """);

        Assert.That(diagnostic.GetMessage(), Does.Contain("parameters after EndpointConfiguration must be IServiceCollection, IConfiguration, or IHostEnvironment"));
    }

    [Test]
    public void ReportsAllProblemsInSingleDiagnostic()
    {
        var diagnostic = GetInvalidSendOnlyEndpointMethodDiagnostic("""
            using NServiceBus;

            namespace Demo;

            public class ClientEndpoint
            {
                [NServiceBusSendOnlyEndpoint("client")]
                public void WrongName(string invalidParam)
                {
                }
            }
            """);

        var message = diagnostic.GetMessage();
        Assert.That(message, Does.Contain("method must be static"));
        Assert.That(message, Does.Contain("method name must be 'Configureclient'"));
        Assert.That(message, Does.Contain("first parameter must be EndpointConfiguration"));
    }

    #region Helpers

    static Diagnostic GetInvalidSendOnlyEndpointMethodDiagnostic(string source)
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(source)
            .SuppressCompilationErrors()
            .SuppressDiagnosticErrors()
            .Run();

        var diagnostics = result.GeneratorDiagnostics;
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.InvalidSendOnlyEndpointMethod),
            "Expected InvalidSendOnlyEndpointMethod diagnostic to be reported");

        return diagnostics.First(d => d.Id == DiagnosticIds.InvalidSendOnlyEndpointMethod);
    }

    #endregion
}
