namespace NServiceBus.AzureFunctions.Analyzer;

using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => InitializeGenerator(context, AzureServiceBusTrigger, AzureServiceBusSendOnlyEndpoint);

    internal static void InitializeGenerator(IncrementalGeneratorInitializationContext context, TriggerDefinition triggerDefinition, SendOnlyEndpointDefinition sendOnlyEndpointDefinition)
    {
        var extractionCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusFunctionAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var sendOnlyExtractionCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusSendOnlyEndpointAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var triggerDefinitionProvider = CreateTriggerDefinitionProvider(context, triggerDefinition);
        var sendOnlyEndpointDefinitionProvider = CreateSendOnlyEndpointDefinitionProvider(context, sendOnlyEndpointDefinition);

        var extractionResults = extractionCandidates
            .Combine(triggerDefinitionProvider)
            .Select(static (pair, ct) => Parser.Extract(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.Extraction);

        var sendOnlyExtractionResults = sendOnlyExtractionCandidates
            .Combine(sendOnlyEndpointDefinitionProvider)
            .Select(static (pair, ct) => Parser.ExtractSendOnly(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.SendOnlyExtraction);

        var diagnostics = extractionResults
            .Collect()
            .Combine(sendOnlyExtractionResults.Collect())
            .SelectMany(static (results, _) =>
            {
                // DiagnosticWithInfo implements structural equality (Location, Info, AdditionalLocations)
                // so HashSet deduplicates correctly. ImmutableEquatableArray enables incremental caching:
                // unchanged documents reuse the same SyntaxTree references, so diagnostics compare equal
                // across steps. Within an edited file, new tree references cause re-reporting, which is
                // correct and cheap.
                var diagnostics = new HashSet<Diagnostic>();
                foreach (var result in results.Left)
                {
                    diagnostics.UnionWith(result.Diagnostics);
                }

                foreach (var sendOnlyResult in results.Right)
                {
                    diagnostics.UnionWith(sendOnlyResult.Diagnostics);
                }
                return diagnostics.ToImmutableEquatableArray();
            })
            .WithTrackingName(TrackingNames.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, diag) =>
            spc.ReportDiagnostic(diag));

        var functionSpecs = extractionResults
            .SelectMany(static (result, _) => result.Functions)
            .WithTrackingName(TrackingNames.Functions);

        var sendOnlyEndpointSpecs = sendOnlyExtractionResults
            .SelectMany(static (result, _) => result.SendOnlyEndpoints)
            .WithTrackingName(TrackingNames.SendOnlyEndpoints);

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToGenerationClassName())
            .WithTrackingName(TrackingNames.AssemblyClassName);

        var combined = functionSpecs.Collect()
            .Combine(sendOnlyEndpointSpecs.Collect())
            .Combine(assemblyClassName)
            .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, data) => Emitter.Emit(spc, data.Left.Left, data.Left.Right, data.Right));

        static IncrementalValueProvider<TriggerDefinition> CreateTriggerDefinitionProvider(
            IncrementalGeneratorInitializationContext context,
            TriggerDefinition triggerDefinition) =>
            context.CompilationProvider.Select((_, _) => triggerDefinition);

        static IncrementalValueProvider<SendOnlyEndpointDefinition> CreateSendOnlyEndpointDefinitionProvider(
            IncrementalGeneratorInitializationContext context,
            SendOnlyEndpointDefinition sendOnlyEndpointDefinition) =>
            context.CompilationProvider.Select((_, _) => sendOnlyEndpointDefinition);
    }
}