namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis.Diagnostics;
using static BuildPropertyNames;

static class ProjectDetection
{
    public static bool IsIsolatedHostFunctionsProject(AnalyzerConfigOptions options)
        => IsExecutableProject(options) && IsIsolatedFunctionsProject(options);

    // We intentionally key on FunctionsExecutionModel instead of AzureFunctionsVersion so detection
    // stays stable if the Functions SDK updates version labels (for example, v4 -> v5).
    static bool IsIsolatedFunctionsProject(AnalyzerConfigOptions options)
        => options.TryGetValue(FunctionsExecutionModel, out var executionModel)
           && string.Equals(executionModel, IsolatedExecutionModel, StringComparison.OrdinalIgnoreCase);

    static bool IsExecutableProject(AnalyzerConfigOptions options)
        => options.TryGetValue(OutputType, out var outputType)
           && string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase);

    public static string? GetRootNamespace(AnalyzerConfigOptions options)
    {
        if (!options.TryGetValue(RootNamespace, out var rootNamespace)
            || string.IsNullOrWhiteSpace(rootNamespace))
        {
            return null;
        }

        return rootNamespace;
    }

    const string IsolatedExecutionModel = "isolated";
}