namespace NServiceBus.AzureFunctions.Analyzer;

using Core.Analyzer;
using Microsoft.CodeAnalysis;

interface IDiagnosticsSpec
{
    ImmutableEquatableArray<Diagnostic> Diagnostics { get; }
}