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
            writer.WithFileScopedNamespace(composition.RootNamespace);
            writer.WithGeneratedCodeAttribute();
            writer.WriteLine("internal static class NServiceBusFunctionsComposition");
            writer.WriteLine("{");
            writer.Indentation++;
            writer.WriteLine("extension(global::Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder builder)");
            writer.WriteLine("{");
            writer.Indentation++;
            writer.WriteLine("public void AddNServiceBusFunctions()");
            writer.WriteLine("{");
            writer.Indentation++;
            writer.WriteLine("global::System.ArgumentNullException.ThrowIfNull(builder);");
            writer.WriteLine();

            foreach (var registrationClass in composition.RegistrationClasses)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (registrationClass.Kind == RegistrationClassKind.Function)
                {
                    writer.WriteLine($"foreach (var manifest in global::{registrationClass.FullClassName}.GetFunctionManifests())");
                    writer.WriteLine("{");
                    writer.WriteLine("    manifest.Register(builder, manifest);");
                    writer.WriteLine("}");
                }
                else
                {
                    writer.WriteLine($"foreach (var manifest in global::{registrationClass.FullClassName}.GetSendOnlyEndpointManifests())");
                    writer.WriteLine("{");
                    writer.WriteLine("    manifest.Register(builder, manifest);");
                    writer.WriteLine("}");
                }
            }

            writer.CloseCurlies();
            context.AddSource(TrackingNames.Composition, writer.ToSourceText());
        }
    }
}
