namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class SendOnlyEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var extractionCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusSendOnlyEndpointAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var extractionResults = extractionCandidates
            .Combine(context.CompilationProvider.Select(static (_, _) => new SendOnlyEndpointDefinition()))
            .Select(static (pair, ct) => Parser.Extract(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.Extraction);

        var diagnostics = extractionResults
            .Collect()
            .SelectMany(static (results, _) => results.ToDiagnostics())
            .WithTrackingName(TrackingNames.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, diag) => spc.ReportDiagnostic(diag));

        var sendOnlyEndpointSpecs = extractionResults
            .SelectMany(static (result, _) => result.SendOnlyEndpoints)
            .WithTrackingName(TrackingNames.SendOnlyEndpoints);

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToSendOnlyGenerationClassName())
            .WithTrackingName(TrackingNames.AssemblyClassName);

        var combined = sendOnlyEndpointSpecs.Collect()
            .Combine(assemblyClassName)
            .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, data) => Emitter.Emit(spc, data.Left, data.Right));
    }
}