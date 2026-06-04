namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using Core.Analyzer;
using Microsoft.CodeAnalysis;

static class DiagnosticsSpecExtensions
{
    public static IEnumerable<Diagnostic> ToDiagnostics<T>(this ImmutableArray<T> results) where T : IDiagnosticsSpec
    {
        // DiagnosticWithInfo implements structural equality (Location, Info, AdditionalLocations)
        // so HashSet deduplicates correctly. ImmutableEquatableArray enables incremental caching:
        // unchanged documents reuse the same SyntaxTree references, so diagnostics compare equal
        // across steps. Within an edited file, new tree references cause re-reporting, which is
        // correct and cheap.
        var diagnostics = new HashSet<Diagnostic>();
        foreach (var result in results)
        {
            diagnostics.UnionWith(result.Diagnostics);
        }

        return diagnostics.ToImmutableEquatableArray();
    }
}