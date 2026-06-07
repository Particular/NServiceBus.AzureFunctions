namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

static class HostProjectPipeline
{
    public static IncrementalValueProvider<HostProjectSpec> Build(IncrementalGeneratorInitializationContext context) =>
        context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => Parser.ParseHostProject(provider))
            .WithTrackingName(FunctionCompositionGenerator.TrackingNames.HostProject);

    static class Parser
    {
        internal static HostProjectSpec ParseHostProject(AnalyzerConfigOptionsProvider provider)
        {
            var options = provider.GlobalOptions;
            var isHostProject = ProjectDetection.IsIsolatedHostFunctionsProject(options);
            var effectiveRootNameSpace = ProjectDetection.GetRootNamespace(options);

            return new HostProjectSpec(isHostProject, effectiveRootNameSpace);
        }
    }
}