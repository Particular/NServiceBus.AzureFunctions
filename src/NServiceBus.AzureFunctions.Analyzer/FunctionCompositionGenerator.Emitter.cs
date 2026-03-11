namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Utility;

public sealed partial class FunctionCompositionGenerator
{
    static class Emitter
    {
        public static void Emit(SourceProductionContext context, CompositionSpec? composition)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (composition is null)
            {
                return;
            }

            var writer = new SourceWriter();
            writer.PreAmble();
            writer.WithOpenNamespace(composition.RootNamespace);
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

            foreach (var registrationClass in composition.RegistrationClasses)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                writer.WriteLine($"foreach (var manifest in global::{registrationClass.FullClassName}.GetFunctionManifests())");
                writer.WriteLine("    global::NServiceBus.FunctionsHostApplicationBuilderExtensions.AddNServiceBusFunction(builder, manifest);");
            }

            writer.CloseCurlies();
            context.AddSource(TrackingNames.Composition, writer.ToSourceText());
        }
    }
}