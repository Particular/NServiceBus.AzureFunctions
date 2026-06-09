namespace NServiceBus.AzureFunctions.Analyzer;

using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionCompositionInterceptor : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var addNServiceBusFunctions = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => AddNServiceBusFunctionsDetection.SyntaxLooksLikeInvocation(node),
                transform: static (ctx, cancellationToken) => AddNServiceBusFunctionsDetection.ParseInvocation((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, cancellationToken))
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

    internal readonly record struct InterceptableCompositionSpecs(ImmutableEquatableArray<AddNServiceBusFunctionsInvocationSpec> Specs);
}