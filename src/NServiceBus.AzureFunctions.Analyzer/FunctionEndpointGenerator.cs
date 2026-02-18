#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

[Generator]
public sealed class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var functionInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NServiceBus.NServiceBusFunctionAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax or MethodDeclarationSyntax,
                transform: static (ctx, ct) => ExtractFunctionInfos(ctx, ct))
            .SelectMany(static (infos, _) => infos);

        var sendOnlyInfos = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NServiceBus.NServiceBusSendOnlyEndpointAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ExtractSendOnlyInfo(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value);

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToGenerationClassName());

        var combined = functionInfos.Collect()
            .Combine(sendOnlyInfos.Collect())
            .Combine(assemblyClassName);

        context.RegisterSourceOutput(combined, static (spc, data) =>
            GenerateSource(spc, data.Left.Left, data.Left.Right, data.Right));
    }

    static ImmutableArray<FunctionInfo> ExtractFunctionInfos(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Attributes.Length == 0)
        {
            return ImmutableArray<FunctionInfo>.Empty;
        }

        var attr = context.Attributes[0];

        // Extract explicit config type from typeof() argument, if provided
        INamedTypeSymbol? explicitConfigType = null;
        if (attr.ConstructorArguments.Length > 0)
        {
            explicitConfigType = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
        }

        if (context.TargetSymbol is INamedTypeSymbol classSymbol)
        {
            // Class-level: infer config type from the class itself when no typeof()
            var configType = explicitConfigType ?? classSymbol;
            var configTypeFullName = configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var results = ImmutableArray.CreateBuilder<FunctionInfo>();
            foreach (var member in classSymbol.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is IMethodSymbol method)
                {
                    var info = TryExtractFromMethod(method, configTypeFullName);
                    if (info is not null)
                    {
                        results.Add(info.Value);
                    }
                }
            }

            return results.ToImmutable();
        }

        if (context.TargetSymbol is IMethodSymbol methodSymbol)
        {
            // Method-level: infer config type from the containing class when no typeof()
            var configType = explicitConfigType ?? methodSymbol.ContainingType;
            var configTypeFullName = configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var info = TryExtractFromMethod(methodSymbol, configTypeFullName);
            return info is not null
                ? ImmutableArray.Create(info.Value)
                : ImmutableArray<FunctionInfo>.Empty;
        }

        return ImmutableArray<FunctionInfo>.Empty;
    }

    static SendOnlyEndpointInfo? ExtractSendOnlyInfo(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol classSymbol)
        {
            return null;
        }

        var attr = context.Attributes[0];

        string? endpointName = null;
        if (attr.ConstructorArguments.Length > 0)
        {
            endpointName = attr.ConstructorArguments[0].Value as string;
        }

        // Extract explicit config type from typeof() argument, if provided
        INamedTypeSymbol? explicitConfigType = null;
        if (attr.ConstructorArguments.Length > 1)
        {
            explicitConfigType = attr.ConstructorArguments[1].Value as INamedTypeSymbol;
        }

        endpointName ??= classSymbol.Name;

        var configType = explicitConfigType ?? classSymbol;

        var configTypeFullName = configType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new SendOnlyEndpointInfo(endpointName, configTypeFullName);
    }

    static FunctionInfo? TryExtractFromMethod(IMethodSymbol method, string configTypeFullName)
    {
        var functionAttr = method.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Microsoft.Azure.Functions.Worker.FunctionAttribute");
        if (functionAttr is null || functionAttr.ConstructorArguments.Length == 0)
        {
            return null;
        }

        if (functionAttr.ConstructorArguments[0].Value is not string functionName)
        {
            return null;
        }

        string? queueName = null;
        string? connectionName = null;
        string? messageParamName = null;
        string? functionContextParamName = null;
        string? cancellationTokenParamName = null;

        var paramList = new StringBuilder();

        for (int i = 0; i < method.Parameters.Length; i++)
        {
            var param = method.Parameters[i];
            if (i > 0)
            {
                paramList.Append(",\n            ");
            }

            var paramTypeFqn = param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            paramList.Append(paramTypeFqn).Append(' ').Append(param.Name);

            foreach (var pAttr in param.GetAttributes())
            {
                if (pAttr.AttributeClass?.ToDisplayString() == "Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute")
                {
                    if (pAttr.ConstructorArguments.Length > 0)
                    {
                        queueName = pAttr.ConstructorArguments[0].Value as string;
                    }

                    foreach (var namedArg in pAttr.NamedArguments)
                    {
                        if (namedArg.Key == "Connection")
                        {
                            connectionName = namedArg.Value.Value as string;
                        }
                    }

                    messageParamName = param.Name;
                }
            }

            if (param.Type.ToDisplayString() == "Microsoft.Azure.Functions.Worker.FunctionContext")
            {
                functionContextParamName = param.Name;
            }

            if (param.Type.ToDisplayString() == "System.Threading.CancellationToken")
            {
                cancellationTokenParamName = param.Name;
            }
        }

        if (queueName is null || functionContextParamName is null || messageParamName is null)
        {
            return null;
        }

        connectionName ??= "";
        cancellationTokenParamName ??= "cancellationToken";

        var containingType = method.ContainingType;
        var ns = containingType.ContainingNamespace.ToDisplayString();
        var className = containingType.Name;
        var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var accessibility = method.DeclaredAccessibility switch
        {
            Accessibility.NotApplicable => "public",
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "private protected",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.Public => "public",
            _ => "public",
        };

        return new FunctionInfo(
            ns, className, accessibility, method.Name, returnType,
            paramList.ToString(), messageParamName, functionContextParamName,
            cancellationTokenParamName, functionName, queueName, connectionName,
            configTypeFullName);
    }

    static void GenerateSource(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions, ImmutableArray<SendOnlyEndpointInfo> sendOnlyEndpoints, string assemblyClassName)
    {
        if (functions.Length > 0)
        {
            GenerateMethodBodies(spc, functions);
        }

        // Always generate the registration class so the composition generator
        // can discover it by convention for same-project scenarios
        GenerateRegistration(spc, functions, sendOnlyEndpoints, assemblyClassName);
    }

    static void GenerateMethodBodies(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");

        var groups = functions.GroupBy(f => (f.ContainingNamespace, f.ContainingClassName));

        foreach (var group in groups)
        {
            sb.AppendLine();
            sb.AppendLine($"namespace {group.Key.ContainingNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    public partial class {group.Key.ContainingClassName}");
            sb.AppendLine("    {");

            bool first = true;
            foreach (var func in group)
            {
                if (!first)
                {
                    sb.AppendLine();
                }

                first = false;

                sb.AppendLine($"        {func.Accessibility} partial {func.ReturnType} {func.MethodName}(");
                sb.AppendLine($"            {func.ParameterList})");
                sb.AppendLine("        {");
                sb.AppendLine($"            var processor = {func.FunctionContextParamName}.InstanceServices");
                sb.AppendLine($"                .GetKeyedService<global::NServiceBus.AzureFunctions.AzureServiceBus.MessageProcessor>(\"{func.FunctionName}\");");
                sb.AppendLine("            if (processor is null)");
                sb.AppendLine("            {");
                sb.AppendLine($"                throw new global::System.InvalidOperationException(\"{func.FunctionName} has not been registered.\");");
                sb.AppendLine("            }");
                sb.AppendLine($"            return processor.Process({func.MessageParamName}, {func.FunctionContextParamName}, {func.CancellationTokenParamName});");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        spc.AddSource("FunctionMethodBodies.g.cs", sb.ToString());
    }

    static void GenerateRegistration(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions, ImmutableArray<SendOnlyEndpointInfo> sendOnlyEndpoints, string assemblyClassName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("namespace NServiceBus.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine("    [global::NServiceBus.AutoGeneratedFunctionRegistrationsAttribute]");
        sb.AppendLine($"    public static class {assemblyClassName}");
        sb.AppendLine("    {");
        sb.AppendLine("        public static global::System.Collections.Generic.IEnumerable<global::NServiceBus.FunctionManifest>");
        sb.AppendLine("            GetFunctionManifests()");
        sb.AppendLine("        {");

        foreach (var func in functions)
        {
            sb.AppendLine($"            yield return new global::NServiceBus.FunctionManifest(");
            sb.AppendLine($"                \"{func.FunctionName}\", \"{func.QueueName}\", \"{func.ConnectionName}\",");
            sb.AppendLine($"                ec=>{func.ConfigTypeFullName}.Configure{func.FunctionName}(ec));");
        }

        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static global::System.Collections.Generic.IEnumerable<global::NServiceBus.SendOnlyManifest>");
        sb.AppendLine("            GetSendOnlyManifests()");
        sb.AppendLine("        {");

        foreach (var endpoint in sendOnlyEndpoints)
        {
            sb.AppendLine($"            yield return new global::NServiceBus.SendOnlyManifest(");
            sb.AppendLine($"                \"{endpoint.EndpointName}\", ec=>{endpoint.ConfigTypeFullName}.Configure{endpoint.EndpointName}(ec));");
        }

        sb.AppendLine("            yield break;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        spc.AddSource("FunctionRegistration.g.cs", sb.ToString());
    }

    record struct FunctionInfo(
        string ContainingNamespace,
        string ContainingClassName,
        string Accessibility,
        string MethodName,
        string ReturnType,
        string ParameterList,
        string MessageParamName,
        string FunctionContextParamName,
        string CancellationTokenParamName,
        string FunctionName,
        string QueueName,
        string ConnectionName,
        string ConfigTypeFullName);

    record struct SendOnlyEndpointInfo(
        string EndpointName,
        string ConfigTypeFullName);
}