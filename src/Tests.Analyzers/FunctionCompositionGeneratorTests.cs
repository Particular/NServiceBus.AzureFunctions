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
            .Run()
            .Approve()
            .AssertRunsAreEqual();
}