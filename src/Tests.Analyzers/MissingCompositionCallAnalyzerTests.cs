namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using System.Collections.Immutable;
using NServiceBus.AzureFunctions.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
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

        var diagnostics = GetAnalyzerDiagnostics(result);
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

        var diagnostics = GetAnalyzerDiagnostics(result);
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

        var diagnostics = GetAnalyzerDiagnostics(result);
        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == "NSBFUNC004"));
    }

    // TODO we should support this in the analyzer testing package. Seems to be a gap
    static ImmutableArray<Diagnostic> GetAnalyzerDiagnostics(SourceGeneratorTest test)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        var buildField = typeof(SourceGeneratorTest).GetField("build", flags)
            ?? throw new InvalidOperationException("Unable to access SourceGeneratorTest build field.");

        var build = buildField.GetValue(test)
            ?? throw new InvalidOperationException("SourceGeneratorTest build was not initialized.");

        var outputCompilationProperty = build.GetType().GetProperty("OutputCompilation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?? throw new InvalidOperationException("Unable to access source generator output compilation.");

        var compilationWithAnalyzers = (CompilationWithAnalyzers)outputCompilationProperty.GetValue(build)!;

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    static SourceGeneratorTest CreateSourceGeneratorAnalyzerTest() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithAnalyzer<MissingCompositionCallAnalyzer>()
            .BuildAs(OutputKind.ConsoleApplication)
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.AzureFunctionsVersion", "v4")
            .WithProperty("build_property.RootNamespace", "My.FunctionApp")
            .SuppressCompilationErrors();
}