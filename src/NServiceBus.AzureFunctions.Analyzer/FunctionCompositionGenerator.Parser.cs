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
            var options = provider.GlobalOptions;
            // Host detection intentionally combines both checks:
            // - FunctionsExecutionModel == isolated identifies Azure Functions worker projects reliably across Functions version changes.
            // - OutputType == Exe keeps generation scoped to the host executable rather than class libraries.
            var isHostProject = ProjectDetection.IsExecutableProject(options) && ProjectDetection.IsIsolatedFunctionsProject(options);
            var effectiveRootNameSpace = ProjectDetection.GetRootNamespace(options);

            return new HostProjectSpec(isHostProject, effectiveRootNameSpace);
        }

        internal static CompositionSpec? ParseComposition(Compilation compilation, HostProjectSpec hostProject, bool hasLocalFunctions, CancellationToken cancellationToken = default)
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

            if (hasLocalFunctions)
            {
                registrations.Add(CreateGeneratedRegistrationClassSpec(compilation.Assembly));
            }

            if (registrations.Count == 0)
            {
                return null;
            }

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
