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
            .Approve();

    [Test]
    public void ReportsInvalidSendOnlyEndpointMethodWhenMethodIsNotStatic()
    {
        var result = SourceGeneratorTest.ForIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource("""
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
}