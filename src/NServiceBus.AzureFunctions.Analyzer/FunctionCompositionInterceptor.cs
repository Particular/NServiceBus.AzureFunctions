namespace NServiceBus.AzureFunctions.Analyzer;

using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionCompositionInterceptor : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostProject = HostProjectPipeline.Build(context);

        var addNServiceBusFunctions = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => Parser.SyntaxLooksLikeAddNServiceBusFunctionsMethod(node),
                transform: static (ctx, _) => (invocation: (InvocationExpressionSyntax)ctx.Node, semanticModel: ctx.SemanticModel))
            .Combine(hostProject)
            .Where(static pair =>
            {
                var (_, hostProject) = pair;
                return hostProject.IsHostProject;
            })
            .Select(static (pair, cancellationToken) =>
            {
                var ((invocation, semanticModel), hostProject) = pair;
                return Parser.Parse(invocation, semanticModel, hostProject, cancellationToken);
            })
            .Where(static spec => spec.HasValue)
            .Select(static (spec, _) => spec!.Value)
            .WithTrackingName(TrackingNames.AddNServiceBusFunctionsSpec);

        var collected = addNServiceBusFunctions.Collect()
            .Select((specs, _) => new InterceptableCompositionSpecs(specs.ToImmutableEquatableArray()))
            .WithTrackingName(TrackingNames.AddNServiceBusFunctionsSpecs);

        context.RegisterSourceOutput(collected,
            static (productionContext, specs) =>
            {
                var emitter = new Emitter(productionContext);
                emitter.Emit(specs);
            });
    }
}