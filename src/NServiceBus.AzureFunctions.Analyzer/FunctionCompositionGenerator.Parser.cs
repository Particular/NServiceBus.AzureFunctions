namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Analyzer;
using Microsoft.CodeAnalysis;

public sealed partial class FunctionCompositionGenerator
{
    static class Parser
    {
        internal static CompositionSpec? ParseComposition(Compilation compilation, HostProjectSpec hostProject, bool hasLocalFunctions, bool hasLocalSendOnlyEndpoints, CancellationToken cancellationToken = default)
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

                AddGeneratedRegistrationClasses(compilation, referencedAssembly, registrations);
            }

            if (hasLocalFunctions)
            {
                AddGeneratedRegistrationClass(
                    compilation,
                    $"NServiceBus.Generated.{CompilationAssemblyDetails.FromAssembly(compilation.Assembly).ToGenerationClassName()}",
                    RegistrationClassKind.Function,
                    registrations,
                    includeWithoutLookup: true);
            }

            if (hasLocalSendOnlyEndpoints)
            {
                AddGeneratedRegistrationClass(
                    compilation,
                    $"NServiceBus.Generated.{CompilationAssemblyDetails.FromAssembly(compilation.Assembly).ToSendOnlyGenerationClassName()}",
                    RegistrationClassKind.SendOnly,
                    registrations,
                    includeWithoutLookup: true);
            }

            var orderedRegistrations = registrations
                .OrderBy(static registration => registration.FullClassName, StringComparer.Ordinal)
                .ToImmutableEquatableArray();

            // Always emit a CompositionSpec, even when there are no registrations, so the
            // FunctionCompositionInterceptor can safely call NServiceBusGeneratedFunctionsComposition
            // .Register(builder) at every intercepted call site.
            return new CompositionSpec(orderedRegistrations, hostProject.RootNamespace);
        }

        static void AddGeneratedRegistrationClasses(
            Compilation compilation,
            IAssemblySymbol assembly,
            ISet<GeneratedRegistrationClassSpec> registrations,
            bool includeWithoutLookup = false)
        {
            var details = CompilationAssemblyDetails.FromAssembly(assembly);

            AddGeneratedRegistrationClass(compilation, $"NServiceBus.Generated.{details.ToGenerationClassName()}", RegistrationClassKind.Function, registrations, includeWithoutLookup);
            AddGeneratedRegistrationClass(compilation, $"NServiceBus.Generated.{details.ToSendOnlyGenerationClassName()}", RegistrationClassKind.SendOnly, registrations, includeWithoutLookup);
        }

        static void AddGeneratedRegistrationClass(
            Compilation compilation,
            string fullClassName,
            RegistrationClassKind kind,
            ISet<GeneratedRegistrationClassSpec> registrations,
            bool includeWithoutLookup)
        {
            if (includeWithoutLookup || compilation.GetTypeByMetadataName(fullClassName) is not null)
            {
                registrations.Add(new GeneratedRegistrationClassSpec(fullClassName, kind));
            }
        }
    }

    internal readonly record struct GeneratedRegistrationClassSpec(string FullClassName, RegistrationClassKind Kind);
    internal enum RegistrationClassKind
    {
        Function,
        SendOnly
    }

    internal sealed record CompositionSpec(ImmutableEquatableArray<GeneratedRegistrationClassSpec> RegistrationClasses, string? RootNamespace);
}

readonly record struct HostProjectSpec(bool IsHostProject, string? RootNamespace);
