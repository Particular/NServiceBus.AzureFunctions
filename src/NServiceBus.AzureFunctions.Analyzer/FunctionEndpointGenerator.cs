namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Generic;
using System.Collections.Immutable;
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
                "NServiceBus.NServiceBusFunctionAttribute",
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) => ctx);

        var triggerDefinitionProvider = CreateTriggerDefinitionProvider(context, triggerDefinition);

        var extractionResults = extractionCandidates
            .Combine(triggerDefinitionProvider)
            .Select(static (pair, ct) => Parser.Extract(pair.Left, pair.Right, ct))
            .WithTrackingName(TrackingNames.Extraction);

        var diagnostics = extractionResults
            .Collect()
            .SelectMany(static (results, _) =>
            {
                var diagnostics = ImmutableHashSet.CreateBuilder(DiagnosticComparer.Instance);
                foreach (var result in results)
                {
                    diagnostics.UnionWith(result.Diagnostics);
                }

                return diagnostics.ToImmutable();
            })
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

    sealed class DiagnosticComparer : IEqualityComparer<Diagnostic>
    {
        public static DiagnosticComparer Instance { get; } = new();

        public bool Equals(Diagnostic? x, Diagnostic? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return x.Id == y.Id
                   && x.Severity == y.Severity
                   && x.WarningLevel == y.WarningLevel
                   && x.GetMessage() == y.GetMessage()
                   && Equals(x.Location, y.Location);
        }

        public int GetHashCode(Diagnostic obj)
        {
            unchecked
            {
                var hashCode = 17;
                hashCode = (hashCode * 31) + obj.Id.GetHashCode();
                hashCode = (hashCode * 31) + obj.Severity.GetHashCode();
                hashCode = (hashCode * 31) + obj.WarningLevel.GetHashCode();
                hashCode = (hashCode * 31) + obj.GetMessage().GetHashCode();
                hashCode = (hashCode * 31) + (obj.Location?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
