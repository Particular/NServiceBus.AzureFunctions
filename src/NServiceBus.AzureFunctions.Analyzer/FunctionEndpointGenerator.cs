namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var extractionResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NServiceBus.NServiceBusFunctionAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax or MethodDeclarationSyntax,
                transform: static (ctx, ct) => Parser.Extract(ctx, ct))
            .WithTrackingName(TrackingNames.Extraction);

        var diagnostics = extractionResults
            .SelectMany(static (result, _) => result.Diagnostics)
            .WithTrackingName(TrackingNames.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, diag) =>
            Emitter.ReportDiagnostic(spc, diag));

        var functionSpecs = extractionResults
            .SelectMany(static (result, _) => result.Functions)
            .WithTrackingName(TrackingNames.Functions);

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToGenerationClassName())
            .WithTrackingName(TrackingNames.AssemblyClassName);

        var combined = functionSpecs.Collect()
            .Combine(assemblyClassName)
            .WithTrackingName(TrackingNames.Combined);

        context.RegisterSourceOutput(combined, static (spc, data) =>
            Emitter.Emit(spc, data.Left, data.Right));
    }
}