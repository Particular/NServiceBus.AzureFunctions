#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using NServiceBus.AzureFunctions.Analyzer.Utility;

[Generator]
public sealed class FunctionCompositionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var hostProjectInfo = context.AnalyzerConfigOptionsProvider.Select((provider, _) =>
        {
            provider.GlobalOptions.TryGetValue("build_property.OutputType", out var outputType);
            provider.GlobalOptions.TryGetValue("build_property.AzureFunctionsVersion", out var azureFunctionsVersion);
            provider.GlobalOptions.TryGetValue("build_property.RootNamespace", out var rootNamespace);

            var isHost = string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(azureFunctionsVersion);

            return new HostProjectInfo(isHost, rootNamespace);
        });

        var allData = context.CompilationProvider
            .Combine(hostProjectInfo)
            .Select((tuple, ct) =>
            {
                var (compilation, hostInfo) = tuple;

                if (!hostInfo.IsHost)
                {
                    return default;
                }

                var results = ImmutableArray.CreateBuilder<GeneratedRegistrationClass>();
                foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
                {
                    ct.ThrowIfCancellationRequested();
                    var className = CompilationAssemblyDetails.FromAssembly(assembly).ToGenerationClassName();
                    var fullName = $"NServiceBus.Generated.{className}";
                    if (compilation.GetTypeByMetadataName(fullName) is not null)
                    {
                        results.Add(new GeneratedRegistrationClass(fullName));
                    }
                }

                var currentAssemblyClassName = CompilationAssemblyDetails.FromAssembly(compilation.Assembly).ToGenerationClassName();
                results.Add(new GeneratedRegistrationClass($"NServiceBus.Generated.{currentAssemblyClassName}"));

                return new CompositionData(results.ToImmutable().Distinct().ToImmutableArray(), hostInfo.RootNamespace);
            });

        context.RegisterSourceOutput(allData, GenerateCompositionCode);
    }

    static void GenerateCompositionCode(SourceProductionContext spc, CompositionData data)
    {
        if (data == default)
        {
            return;
        }

        var regClasses = data.RegistrationClasses;
        var ns = data.RootNamespace;

        var writer = new SourceWriter();
        writer.PreAmble();
        writer.WriteLine($"namespace {ns};");
        writer.WriteLine();
        writer.WriteLine("public static class NServiceBusFunctionsComposition");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("extension(global::Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder builder)");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("public void AddNServiceBusFunctions()");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("global::System.ArgumentNullException.ThrowIfNull(builder);");
        writer.WriteLine("global::NServiceBus.NServiceBusFunctionsInfrastructure.Initialize(builder);");
        writer.WriteLine();

        foreach (var regClass in regClasses)
        {
            writer.WriteLine($"foreach (var m in global::{regClass.FullClassName}.GetFunctionManifests())");
            writer.WriteLine("    global::NServiceBus.FunctionsHostApplicationBuilderExtensions.AddNServiceBusFunction(builder, m);");
        }
        writer.CloseCurlies();

        spc.AddSource("Composition.g.cs", writer.ToSourceText());
    }

    record struct GeneratedRegistrationClass(string FullClassName);
    record struct HostProjectInfo(bool IsHost, string? RootNamespace);
    record struct CompositionData(
        ImmutableArray<GeneratedRegistrationClass> RegistrationClasses,
        string? RootNamespace);

}