namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed partial class FunctionCompositionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hasLocalFunctions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusFunctionAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0)
            .WithTrackingName(TrackingNames.LocalFunctions);

        var hasLocalSendOnlyEndpoints = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                KnownTypeNames.NServiceBusSendOnlyFunctionAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (_, _) => true)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0)
            .WithTrackingName(TrackingNames.LocalSendOnlyEndpoints);

        var hasAddNServiceBusFunctionsInvocation = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => AddNServiceBusFunctionsParser.SyntaxLooksLikeInvocation(node),
                transform: static (ctx, cancellationToken) => AddNServiceBusFunctionsParser.ParseInvocation((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, cancellationToken))
            .Where(static spec => spec.HasValue)
            .Collect()
            .Select(static (matches, _) => matches.Length > 0)
            .WithTrackingName(TrackingNames.AddNServiceBusFunctionsInvocations);

        var compositions = context.CompilationProvider
            .Combine(hasLocalFunctions)
            .Combine(hasLocalSendOnlyEndpoints)
            .Combine(hasAddNServiceBusFunctionsInvocation)
            .Select(static (data, cancellationToken) => Parser.ParseComposition(data.Left.Left.Left, data.Left.Left.Right, data.Left.Right, data.Right, cancellationToken))
            .WithTrackingName(TrackingNames.Composition);

        context.RegisterSourceOutput(
            compositions,
            static (context, composition) => Emitter.Emit(context, composition));
    }
}