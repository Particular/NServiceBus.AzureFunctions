namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionCompositionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostProject = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => Parser.ParseHostProject(provider))
            .WithTrackingName(TrackingNames.HostProject);

        var hasLocalFunctions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NServiceBus.NServiceBusFunctionAttribute",
                static (node, _) => node is MethodDeclarationSyntax,
                static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0)
            .WithTrackingName(TrackingNames.LocalFunctions);

        var compositions = context.CompilationProvider
            .Combine(hostProject)
            .Combine(hasLocalFunctions)
            .Select(static (data, cancellationToken) => Parser.ParseComposition(data.Left.Left, data.Left.Right, data.Right, cancellationToken))
            .WithTrackingName(TrackingNames.Composition);

        context.RegisterSourceOutput(
            compositions,
            static (context, composition) => Emitter.Emit(context, composition));
    }
}