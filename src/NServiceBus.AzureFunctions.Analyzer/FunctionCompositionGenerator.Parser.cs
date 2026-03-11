#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

public sealed partial class FunctionCompositionGenerator
{
    static class Parser
    {
        internal static HostProjectSpec ParseHostProject(AnalyzerConfigOptionsProvider provider)
        {
            provider.GlobalOptions.TryGetValue("build_property.OutputType", out var outputType);
            provider.GlobalOptions.TryGetValue("build_property.AzureFunctionsVersion", out var azureFunctionsVersion);
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);
            var isHostProject = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(azureFunctionsVersion);
            string? effectiveRootNameSpace = string.IsNullOrWhiteSpace(rootNamespace) ? null : rootNamespace;

            return new HostProjectSpec(isHostProject, effectiveRootNameSpace);
        }

        internal static CompositionSpec? ParseComposition(Compilation compilation, HostProjectSpec hostProject, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!hostProject.IsHostProject)
            {
                return null;
            }

            var registrations = new HashSet<GeneratedRegistrationClassSpec>();

            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var registration = CreateGeneratedRegistrationClassSpec(referencedAssembly);
                if (compilation.GetTypeByMetadataName(registration.FullClassName) is not null)
                {
                    registrations.Add(registration);
                }
            }

            registrations.Add(CreateGeneratedRegistrationClassSpec(compilation.Assembly));

            var orderedRegistrations = registrations
                .OrderBy(static registration => registration.FullClassName, StringComparer.Ordinal)
                .ToImmutableEquatableArray();

            return new CompositionSpec(orderedRegistrations, hostProject.RootNamespace);
        }

        static GeneratedRegistrationClassSpec CreateGeneratedRegistrationClassSpec(IAssemblySymbol assembly)
        {
            var className = CompilationAssemblyDetails.FromAssembly(assembly).ToGenerationClassName();
            return new GeneratedRegistrationClassSpec($"NServiceBus.Generated.{className}");
        }
    }

    internal readonly record struct GeneratedRegistrationClassSpec(string FullClassName);
    internal readonly record struct HostProjectSpec(bool IsHostProject, string? RootNamespace);
    internal sealed record CompositionSpec(ImmutableEquatableArray<GeneratedRegistrationClassSpec> RegistrationClasses, string? RootNamespace);
}