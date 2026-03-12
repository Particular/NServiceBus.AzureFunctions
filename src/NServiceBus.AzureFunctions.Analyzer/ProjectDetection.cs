namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static BuildPropertyNames;
using static KnownTypeNames;

static class ProjectDetection
{
    const string IsolatedExecutionModel = "isolated";

    // We intentionally key on FunctionsExecutionModel instead of AzureFunctionsVersion so detection
    // stays stable if the Functions SDK updates version labels (for example, v4 -> v5).
    public static bool IsIsolatedFunctionsProject(AnalyzerConfigOptions options)
        => options.TryGetValue(FunctionsExecutionModel, out var executionModel)
           && string.Equals(executionModel, IsolatedExecutionModel, StringComparison.OrdinalIgnoreCase);

    // In production, FunctionsExecutionModel is the source of truth. The symbol fallback exists only
    // because SourceGeneratorTest currently does not flow WithProperty(...) values to analyzers.
    // Keep this fallback until analyzer config options are propagated in Particular.AnalyzerTesting.
    public static bool IsIsolatedFunctionsProject(Compilation compilation, AnalyzerConfigOptions options)
        => IsIsolatedFunctionsProject(options) || compilation.GetTypeByMetadataName(FunctionAttribute) is not null;

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

static class KnownTypeNames
{
    public const string FunctionAttribute = "Microsoft.Azure.Functions.Worker.FunctionAttribute";
}

static class BuildPropertyNames
{
    public const string FunctionsExecutionModel = "build_property.FunctionsExecutionModel";
    public const string OutputType = "build_property.OutputType";
    public const string RootNamespace = "build_property.RootNamespace";
}