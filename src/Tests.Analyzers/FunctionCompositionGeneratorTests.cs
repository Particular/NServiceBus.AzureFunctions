namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Analyzer;
using NUnit.Framework;
using Particular.AnalyzerTesting;
using static Particular.AnalyzerTesting.GeneratorTestOutput;

[TestFixture]
public class FunctionCompositionGeneratorTests
{
    [Test]
    public void GeneratesProjectComposition() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionCompositionGenerator>()
            .WithIncrementalGenerator<FunctionEndpointGenerator>()
            .WithIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithSource(TestSources.ValidFunction)
            .WithSource(TestSources.ValidSendOnlyEndpoint, "SendOnly.cs")
            .ControlOutput(All)
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "My.FunctionApp")
            .SuppressCompilationErrors()
            .Run()
            .Approve();
}