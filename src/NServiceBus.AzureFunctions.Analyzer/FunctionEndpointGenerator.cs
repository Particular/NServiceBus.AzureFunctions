namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) => InitializeGenerator<AzureServiceBusTriggerDefinition>(context);

    // This method currently exists to proof that technically the generator pipeline can be extended with other trigger definitions
    // without having to change the analyzer. In practice, we don't have any other trigger definitions at the moment,
    // but there are tests that verify it would be possible to add more. There might still be some refactoring opportunities to make the extension story better
    // but this is good enough for now.
    internal static void InitializeGenerator<TDefinition>(IncrementalGeneratorInitializationContext context)
        where TDefinition : TriggerDefinition, new()
    {
        var extractionCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusFunctionAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var extractionResults = extractionCandidates
            .Combine(context.CompilationProvider.Select(static (_, _) => new TDefinition()))
            .Select(static (pair, ct) => Parser.Extract(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.Extraction);

        var diagnostics = extractionResults
            .Collect()
            .SelectMany(static (results, _) => results.ToDiagnostics())
            .WithTrackingName(TrackingNames.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, diag) =>
            spc.ReportDiagnostic(diag));

        var functionSpecs = extractionResults
            .SelectMany(static (result, _) => result.Functions)
            .WithTrackingName(TrackingNames.Functions);

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToGenerationClassName())
            .WithTrackingName(TrackingNames.AssemblyClassName);

        var combined = functionSpecs.Collect()
            .Combine(assemblyClassName)
            .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, data) => Emitter.Emit(spc, data.Left, data.Right));
    }
}