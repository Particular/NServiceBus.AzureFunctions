namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using NServiceBus.AzureFunctions.Analyzer;
using Microsoft.CodeAnalysis;
using NUnit.Framework;
using Particular.AnalyzerTesting;

[TestFixture]
public class MissingCompositionCallAnalyzerTests
{
    [Test]
    public void ReportsDiagnosticWhenCompositionCallIsMissing()
    {
        var result = CreateSourceGeneratorAnalyzerTest()
            .WithSource(TestSources.ValidFunction)
            .Run();

        var diagnostics = result.GetAnalyzerDiagnostics();
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == "NSBFUNC004"));
    }

    [Test]
    public void ReportsDiagnosticWhenFakeCompositionCallIsOutsideRootNamespace()
    {
        const string fakeCall = """
            public class Program
            {
                public void Configure()
                {
                    var builder = new Builder();
                    builder.AddNServiceBusFunctions();
                }
            }

            public class Builder;

            public static class NServiceBusFunctionsComposition
            {
                public static void AddNServiceBusFunctions(this Builder builder)
                {
                }
            }
            """;

        var result = CreateSourceGeneratorAnalyzerTest()
            .WithSource(TestSources.ValidFunction)
            .WithSource(fakeCall, "Program.cs")
            .Run();

        var diagnostics = result.GetAnalyzerDiagnostics();
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == "NSBFUNC004"));
    }

    [Test]
    public void DoesNotReportDiagnosticWhenCompositionCallIsInRootNamespace()
    {
        const string validCall = """
            using Microsoft.Azure.Functions.Worker.Builder;
            using My.FunctionApp;

            namespace Demo;

            public static class Startup
            {
                public static void Configure(FunctionsApplicationBuilder builder)
                {
                    builder.AddNServiceBusFunctions();
                }
            }
            """;

        var result = CreateSourceGeneratorAnalyzerTest()
            .WithSource(TestSources.ValidFunction)
            .WithSource(validCall, "Startup.cs")
            .Run();

        var diagnostics = result.GetAnalyzerDiagnostics();
        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == "NSBFUNC004"));
    }

    static SourceGeneratorTest CreateSourceGeneratorAnalyzerTest() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithAnalyzer<MissingCompositionCallAnalyzer>()
            .BuildAs(OutputKind.ConsoleApplication)
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "My.FunctionApp")
            .SuppressCompilationErrors();
}