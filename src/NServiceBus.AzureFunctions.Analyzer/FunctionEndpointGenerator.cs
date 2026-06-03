namespace NServiceBus.AzureFunctions.Analyzer;

using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => InitializeGenerator(context, AzureServiceBusTrigger);

    internal static void InitializeGenerator(IncrementalGeneratorInitializationContext context, TriggerDefinition triggerDefinition)
    {
        var extractionCandidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusFunctionAttribute,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var triggerDefinitionProvider = CreateTriggerDefinitionProvider(context, triggerDefinition);

        var extractionResults = extractionCandidates
            .Combine(triggerDefinitionProvider)
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

        static IncrementalValueProvider<TriggerDefinition> CreateTriggerDefinitionProvider(
            IncrementalGeneratorInitializationContext context,
            TriggerDefinition triggerDefinition) =>
            context.CompilationProvider.Select((_, _) => triggerDefinition);
    }
}