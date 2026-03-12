namespace NServiceBus.AzureFunctions.Analyzers.Tests;

using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Particular.AnalyzerTesting;

public static class SourceGeneratorTestHelpers
{
    extension(SourceGeneratorTest test)
    {
        public ImmutableArray<Diagnostic> GetGeneratorDiagnostics()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
                                                         System.Reflection.BindingFlags.NonPublic;

            var buildField = typeof(SourceGeneratorTest).GetField("build", flags)
                             ?? throw new InvalidOperationException("Unable to access SourceGeneratorTest build field.");

            var build = buildField.GetValue(test)
                        ?? throw new InvalidOperationException("SourceGeneratorTest build was not initialized.");

            var generatorDiagnosticsProperty = build.GetType().GetProperty("GeneratorDiagnostics",
                                                   System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.Public)
                                               ?? throw new InvalidOperationException(
                                                   "Unable to access source generator diagnostics.");

            return (ImmutableArray<Diagnostic>)generatorDiagnosticsProperty.GetValue(build)!;
        }

        public ImmutableArray<Diagnostic> GetAnalyzerDiagnostics()
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
    }
}