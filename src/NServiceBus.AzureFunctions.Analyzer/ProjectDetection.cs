namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static BuildPropertyNames;

static class ProjectDetection
{
    const string IsolatedExecutionModel = "isolated";

    public static bool IsIsolatedFunctionsProject(AnalyzerConfigOptions options)
        => options.TryGetValue(FunctionsExecutionModel, out var executionModel)
           && string.Equals(executionModel, IsolatedExecutionModel, StringComparison.OrdinalIgnoreCase);

    public static bool IsIsolatedFunctionsProject(Compilation compilation, AnalyzerConfigOptions options)
        => IsIsolatedFunctionsProject(options)
           // Temporary hack for the analyzer tests
           || compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionAttribute") is not null;

    public static bool IsExecutableProject(AnalyzerConfigOptions options)
        => options.TryGetValue(OutputType, out var outputType)
           && string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase);

    public static bool IsExecutableProject(Compilation compilation, AnalyzerConfigOptions options)
        => IsExecutableProject(options)
           || compilation.Options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication;

    public static string? GetRootNamespace(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(RootNamespace, out var rootNamespace)
            || string.IsNullOrWhiteSpace(rootNamespace))
        {
            return null;
        }

        return rootNamespace;
    }
}

static class BuildPropertyNames
{
    public const string FunctionsExecutionModel = "build_property.FunctionsExecutionModel";
    public const string OutputType = "build_property.OutputType";
    public const string RootNamespace = "build_property.RootNamespace";
}