namespace NServiceBus.AzureFunctions.Analyzer;

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Utility;

public sealed partial class FunctionCompositionInterceptor
{
    internal class Emitter(SourceProductionContext sourceProductionContext)
    {
        public void Emit(InterceptableCompositionSpecs specs) => Emit(sourceProductionContext, specs);

        static void Emit(SourceProductionContext context, InterceptableCompositionSpecs specs)
        {
            if (specs.Specs.Count == 0)
            {
                return;
            }

            var sourceWriter = new SourceWriter()
                .ForInterceptor();

            // The InterceptsLocationAttribute shim type lives under System.Runtime.CompilerServices
            // (already emitted by ForInterceptor) and the interception class lives under NServiceBus
            // (so the InterceptorsNamespaces MSBuild property must include NServiceBus).
            sourceWriter.WriteLine("namespace NServiceBus");
            sourceWriter.WriteLine("{");
            sourceWriter.Indentation++;

            sourceWriter.WithGeneratedCodeAttribute();
            sourceWriter.WriteLine("static file class InterceptionsOfAddNServiceBusFunctionsMethod");
            sourceWriter.WriteLine("{");
            sourceWriter.Indentation++;

            sourceWriter.WriteLine("extension (Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder builder)");
            sourceWriter.WriteLine("{");
            sourceWriter.Indentation++;

            // Group specs by the resolved method name so multiple call sites share a single
            // generated method with multiple [InterceptsLocation] attributes, mirroring the
            // AddHandlerInterceptor behavior.
            var groups = specs.Specs
                .Select(spec => (MethodName: BuildMethodName(spec.RootNamespace), Spec: spec))
                .GroupBy(item => item.MethodName, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToArray();

            for (var index = 0; index < groups.Length; index++)
            {
                var group = groups[index];
                (string MethodName, InterceptableCompositionSpec Spec)? first = null;
                foreach (var item in group)
                {
                    first ??= item;
                    sourceWriter.WriteLine($"{item.Spec.LocationSpec.Attribute} // {item.Spec.LocationSpec.DisplayLocation}");
                }

                if (!first.HasValue)
                {
                    continue;
                }

                var (methodName, firstSpec) = first.Value;
                sourceWriter.WriteLine($"public void {methodName}()");
                sourceWriter.WriteLine("{");
                sourceWriter.Indentation++;

                sourceWriter.WriteLine("System.ArgumentNullException.ThrowIfNull(builder);");
                sourceWriter.WriteLine($"{BuildRegisterCall(firstSpec.RootNamespace)};");

                sourceWriter.Indentation--;
                sourceWriter.WriteLine("}");

                if (index < groups.Length - 1)
                {
                    sourceWriter.WriteLine();
                }
            }

            sourceWriter.CloseCurlies();
            context.AddSource("InterceptionsOfAddNServiceBusFunctionsMethod.g.cs", sourceWriter.ToSourceText());
        }

        static string BuildMethodName(string? rootNamespace) =>
            InterceptorMethodNameBuilder.Build(
                "AddNServiceBusFunctions_",
                "AddNServiceBusFunctions",
                $"{rootNamespace ?? "global"}.{KnownTypeNames.GeneratedFunctionsCompositionClassName}");

        static string BuildRegisterCall(string? rootNamespace)
        {
            var qualified = string.IsNullOrWhiteSpace(rootNamespace)
                ? KnownTypeNames.GeneratedFunctionsCompositionClassName
                : $"{rootNamespace}.{KnownTypeNames.GeneratedFunctionsCompositionClassName}";
            return $"{qualified}.{KnownTypeNames.GeneratedFunctionsCompositionRegisterMethodName}(builder)";
        }
    }
}