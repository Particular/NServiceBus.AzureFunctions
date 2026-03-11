namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

[Generator]
public sealed partial class FunctionCompositionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostProject = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => Parser.ParseHostProject(provider))
            .WithTrackingName(TrackingNames.HostProject);

        var compositions = context.CompilationProvider
            .Combine(hostProject)
            .Select(static (data, cancellationToken) => Parser.ParseComposition(data.Left, data.Right, cancellationToken))
            .WithTrackingName(TrackingNames.Composition);

        context.RegisterSourceOutput(
            compositions,
            static (context, composition) => Emitter.Emit(context, composition));
    }
}