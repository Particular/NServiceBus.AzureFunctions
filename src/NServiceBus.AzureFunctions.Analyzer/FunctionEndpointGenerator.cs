#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NServiceBus.AzureFunctions.Analyzer.Utility;

[Generator]
public sealed class FunctionEndpointGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var extractionResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "NServiceBus.NServiceBusFunctionAttribute",
                predicate: static (node, _) => node is ClassDeclarationSyntax or MethodDeclarationSyntax,
                transform: static (ctx, ct) => ExtractFunctionInfos(ctx, ct));

        var functionInfos = extractionResults.SelectMany(static (result, _) => result.FunctionInfos);
        var diagnostics = extractionResults.SelectMany(static (result, _) => result.Diagnostics);

        context.RegisterSourceOutput(diagnostics, static (spc, diagnostic) => spc.ReportDiagnostic(diagnostic));

        var assemblyClassName = context.CompilationProvider
            .Select(static (c, _) => CompilationAssemblyDetails.FromAssembly(c.Assembly).ToGenerationClassName());

        var combined = functionInfos.Collect()
            .Combine(assemblyClassName);

        context.RegisterSourceOutput(combined, static (spc, data) =>
            GenerateSource(spc, data.Left, data.Right));
    }

    static FunctionExtractionResult ExtractFunctionInfos(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.Attributes.Length == 0)
        {
            return FunctionExtractionResult.Empty;
        }

        if (!FunctionEndpointGeneratorKnownTypes.TryGet(context.SemanticModel.Compilation, out var knownTypes))
        {
            return FunctionExtractionResult.Empty;
        }

        if (context.TargetSymbol is INamedTypeSymbol classSymbol)
        {
            var results = ImmutableArray.CreateBuilder<FunctionInfo>();
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            if (!IsPartial(classSymbol, cancellationToken))
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ClassMustBePartialDescriptor, classSymbol, classSymbol.Name));
            }

            var implementsIHandleMessages = classSymbol.AllInterfaces
                .Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, knownTypes.IHandleMessages));

            if (implementsIHandleMessages)
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ShouldNotImplementIHandleMessagesDescriptor, classSymbol, classSymbol.Name));
            }

            foreach (var member in classSymbol.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is IMethodSymbol method)
                {
                    var info = TryExtractFromMethod(method, knownTypes);
                    if (info is not null)
                    {
                        results.Add(info.Value);
                    }
                }
            }

            return new FunctionExtractionResult(results.ToImmutable(), diagnostics.ToImmutable());
        }

        if (context.TargetSymbol is IMethodSymbol methodSymbol)
        {
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            if (!methodSymbol.IsPartialDefinition)
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.MethodMustBePartialDescriptor, methodSymbol, methodSymbol.Name));
            }

            if (!IsPartial(methodSymbol.ContainingType, cancellationToken))
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ClassMustBePartialDescriptor, methodSymbol.ContainingType, methodSymbol.ContainingType.Name));
            }

            var info = TryExtractFromMethod(methodSymbol, knownTypes);
            var infos = info is not null
                ? ImmutableArray.Create(info.Value)
                : ImmutableArray<FunctionInfo>.Empty;

            return new FunctionExtractionResult(infos, diagnostics.ToImmutable());
        }

        return FunctionExtractionResult.Empty;
    }

    static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
        => type.DeclaringSyntaxReferences.Any(r =>
            r.GetSyntax(cancellationToken) is ClassDeclarationSyntax classDeclaration
            && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, ISymbol symbol, string argument)
        => Diagnostic.Create(descriptor, symbol.Locations.FirstOrDefault(), argument);

    static IMethodSymbol GetConfigureMethodInfo(INamedTypeSymbol functionClassType, string endpointName, FunctionEndpointGeneratorKnownTypes knownTypes)
    {
        var configureMethodName = $"Configure{endpointName}";

        IMethodSymbol? configureMethod = null;
        foreach (var member in functionClassType.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                if (method.Name == configureMethodName)
                {
                    if (configureMethod is not null)
                    {
                        throw new InvalidOperationException($"Multiple {configureMethodName} configuration methods found on {functionClassType.Name}");
                    }

                    configureMethod = method;
                }
            }
        }

        if (configureMethod is null)
        {
            throw new InvalidOperationException($"Method {configureMethodName} not found on {functionClassType.Name}");
        }

        var parameters = configureMethod.Parameters;

        if (parameters.Length == 0)
        {
            throw new InvalidOperationException($"Method {configureMethodName} must have a `EndpointConfiguration` parameter");
        }

        if (!SymbolEqualityComparer.Default.Equals(parameters[0].Type, knownTypes.EndpointConfiguration))
        {
            throw new InvalidOperationException($"Method {configureMethodName} must have `EndpointConfiguration` as the first parameter");
        }

        var optionalParameters = parameters.Skip(1);

        foreach (var parameter in optionalParameters)
        {
            var isAllowedOptionalParameter =
                SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IConfiguration)
                || SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IHostEnvironment);

            if (!isAllowedOptionalParameter)
            {
                throw new InvalidOperationException($"Method {configureMethodName} contains unsupported parameter {parameter.Type.ToDisplayString()}");
            }
        }

        return configureMethod;
    }

    static FunctionInfo? TryExtractFromMethod(IMethodSymbol method, FunctionEndpointGeneratorKnownTypes knownTypes)
    {
        var functionAttr = method.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, knownTypes.FunctionAttribute));
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
                if (SymbolEqualityComparer.Default.Equals(pAttr.AttributeClass, knownTypes.ServiceBusTriggerAttribute))
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

            if (SymbolEqualityComparer.Default.Equals(param.Type, knownTypes.FunctionContext))
            {
                functionContextParamName = param.Name;
            }

            if (SymbolEqualityComparer.Default.Equals(param.Type, knownTypes.CancellationToken))
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

        var configureMethodInfo = GetConfigureMethodInfo(containingType, functionName, knownTypes);

        return new FunctionInfo(
            ns, className, accessibility, method.Name, returnType,
            paramList.ToString(), messageParamName, functionContextParamName,
            cancellationTokenParamName, functionName, queueName, connectionName,
            configureMethodInfo);
    }

    static void GenerateSource(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions, string assemblyClassName)
    {
        if (functions.Length > 0)
        {
            GenerateMethodBodies(spc, functions);
        }

        // Always generate the registration class so the composition generator
        // can discover it by convention for same-project scenarios
        GenerateRegistration(spc, functions, assemblyClassName);
    }

    static void GenerateMethodBodies(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions)
    {
        var writer = new SourceWriter();
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine("using Microsoft.Extensions.DependencyInjection;");

        var groups = functions.GroupBy(f => (f.ContainingNamespace, f.ContainingClassName));

        foreach (var group in groups)
        {
            writer.WriteLine();
            writer.WriteLine($"namespace {group.Key.ContainingNamespace}");
            writer.WriteLine("{");
            writer.Indentation++;
            writer.WriteLine($"public partial class {group.Key.ContainingClassName}");
            writer.WriteLine("{");
            writer.Indentation++;

            bool first = true;
            foreach (var func in group)
            {
                if (!first)
                {
                    writer.WriteLine();
                }

                first = false;

                writer.WriteLine($"{func.Accessibility} partial {func.ReturnType} {func.MethodName}(");
                writer.WriteLine($"    {func.ParameterList})");
                writer.WriteLine("{");
                writer.Indentation++;
                writer.WriteLine($"var processor = {func.FunctionContextParamName}.InstanceServices");
                writer.WriteLine($"    .GetKeyedService<global::NServiceBus.AzureFunctions.AzureServiceBus.MessageProcessor>(\"{func.FunctionName}\");");
                writer.WriteLine("if (processor is null)");
                writer.WriteLine("{");
                writer.Indentation++;
                writer.WriteLine($"throw new global::System.InvalidOperationException(\"{func.FunctionName} has not been registered.\");");
                writer.Indentation--;
                writer.WriteLine("}");
                writer.WriteLine($"return processor.Process({func.MessageParamName}, {func.FunctionContextParamName}, {func.CancellationTokenParamName});");
                writer.Indentation--;
                writer.WriteLine("}");
            }

            writer.Indentation--;
            writer.WriteLine("}");
            writer.Indentation--;
            writer.WriteLine("}");
        }

        spc.AddSource("FunctionMethodBodies.g.cs", writer.ToSourceText());
    }

    static void GenerateRegistration(SourceProductionContext spc, ImmutableArray<FunctionInfo> functions, string assemblyClassName)
    {
        var writer = new SourceWriter();
        writer.WriteLine("// <auto-generated/>");
        writer.WriteLine("namespace NServiceBus.Generated");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        writer.WriteLine("[global::NServiceBus.AutoGeneratedFunctionRegistrationsAttribute]");
        writer.WriteLine($"public static class {assemblyClassName}");
        writer.WriteLine("{");
        writer.Indentation++;
        writer.WriteLine("public static global::System.Collections.Generic.IEnumerable<global::NServiceBus.FunctionManifest>");
        writer.WriteLine("    GetFunctionManifests()");
        writer.WriteLine("{");
        writer.Indentation++;

        foreach (var func in functions)
        {
            writer.WriteLine("yield return new global::NServiceBus.FunctionManifest(");
            writer.WriteLine($"    \"{func.FunctionName}\", \"{func.QueueName}\", \"{func.ConnectionName}\",");
            writer.WriteLine($"    {GenerateConfigureMethodCall(func.ConfigureMethod)});");
        }

        writer.WriteLine("yield break;");
        writer.CloseCurlies();

        spc.AddSource("FunctionRegistration.g.cs", writer.ToSourceText());
    }

    static string GenerateConfigureMethodCall(IMethodSymbol configureMethod)
    {
        var parameterNames = configureMethod.Parameters.Select(p => p.Type.Name.ToLower()).ToArray();
        var argumentList = string.Join(", ", parameterNames);
        return $"(endpointconfiguration, iconfiguration, ihostenvironment) => {configureMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{configureMethod.Name}({argumentList})";
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
        IMethodSymbol ConfigureMethod);

    readonly struct FunctionExtractionResult
    {
        public FunctionExtractionResult(ImmutableArray<FunctionInfo> functionInfos, ImmutableArray<Diagnostic> diagnostics)
        {
            FunctionInfos = functionInfos;
            Diagnostics = diagnostics;
        }

        public ImmutableArray<FunctionInfo> FunctionInfos { get; }
        public ImmutableArray<Diagnostic> Diagnostics { get; }

        public static FunctionExtractionResult Empty { get; } = new(
            ImmutableArray<FunctionInfo>.Empty,
            ImmutableArray<Diagnostic>.Empty);
    }

    record struct SendOnlyEndpointInfo(
        string EndpointName,
        IMethodSymbol ConfigureMethod);
}