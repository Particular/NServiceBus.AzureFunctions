namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using System.IO;
using System.Linq;
using Microsoft.Azure.Functions.Worker;
using NServiceBus.AzureFunctions.Analyzer;
using NServiceBus.AzureFunctions.Analyzer.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

        var diagnostics = result.AnalyzerDiagnostics;
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.MissingAddNServiceBusFunctionsCall));
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

        var diagnostics = result.AnalyzerDiagnostics;
        Assert.That(diagnostics, Has.Some.Matches<Diagnostic>(d => d.Id == DiagnosticIds.MissingAddNServiceBusFunctionsCall));
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

        var diagnostics = result.AnalyzerDiagnostics;
        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == DiagnosticIds.MissingAddNServiceBusFunctionsCall));
    }

    [Test]
    public async Task DoesNotReportDiagnosticForNonFunctionsProjectReferencingFunctionsApp()
    {
        var functionsAppReference = BuildFunctionsAppReference();

        var compilation = CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    "public class Tests { }",
                    path: "Tests.cs")
            ],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(FunctionAttribute).Assembly.Location),
                functionsAppReference
            ],
            options: new CSharpCompilationOptions(OutputKind.ConsoleApplication));

        var analyzer = new MissingCompositionCallAnalyzer();
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();

        Assert.That(diagnostics, Has.None.Matches<Diagnostic>(d => d.Id == DiagnosticIds.MissingAddNServiceBusFunctionsCall));
    }

    static SourceGeneratorTest CreateSourceGeneratorAnalyzerTest() =>
        SourceGeneratorTest.ForIncrementalGenerator<FunctionEndpointGenerator>()
            .WithIncrementalGenerator<SendOnlyEndpointGenerator>()
            .WithIncrementalGenerator<FunctionCompositionGenerator>()
            .WithAnalyzer<MissingCompositionCallAnalyzer>()
            .BuildAs(OutputKind.ConsoleApplication)
            .WithProperty("build_property.OutputType", "Exe")
            .WithProperty("build_property.FunctionsExecutionModel", "isolated")
            .WithProperty("build_property.RootNamespace", "My.FunctionApp")
            .SuppressCompilationErrors();

    static MetadataReference BuildFunctionsAppReference()
    {
        const string assemblyName = "ReferencedFunctionsApp";

        var preliminaryCompilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generatedTypeName = $"GeneratedFunctionRegistrations_{assemblyName}_{NonCryptographicHash.GetHash(preliminaryCompilation.Assembly.Identity.GetDisplayName()):x16}";

        var compilation = CSharpCompilation.Create(
            assemblyName: "ReferencedFunctionsApp",
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(
                    $"namespace NServiceBus.Generated; public static class {generatedTypeName} {{ }}",
                    path: "Generated.cs")
            ],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        Assert.That(emitResult.Success, Is.True, string.Join(System.Environment.NewLine, emitResult.Diagnostics.Select(static d => d.ToString())));

        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
