namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using NServiceBus.AzureFunctions.Analyzer;
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
}