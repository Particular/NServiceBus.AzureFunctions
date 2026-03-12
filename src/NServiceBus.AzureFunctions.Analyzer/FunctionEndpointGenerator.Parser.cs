namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

public sealed partial class FunctionEndpointGenerator
{
    static class Parser
    {
        internal static FunctionSpecs Extract(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken = default)
        {
            if (context.Attributes.Length == 0)
            {
                return FunctionSpecs.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!FunctionEndpointGeneratorKnownTypes.TryGet(context.SemanticModel.Compilation, out var knownTypes))
            {
                return FunctionSpecs.Empty;
            }

            return context.TargetSymbol switch
            {
                INamedTypeSymbol classSymbol => ExtractFromClass(classSymbol, knownTypes, cancellationToken),
                IMethodSymbol methodSymbol => ExtractFromMethod(methodSymbol, knownTypes, cancellationToken),
                _ => FunctionSpecs.Empty
            };
        }

        static FunctionSpecs ExtractFromClass(INamedTypeSymbol classSymbol, FunctionEndpointGeneratorKnownTypes knownTypes, CancellationToken cancellationToken)
        {
            var functions = ImmutableArray.CreateBuilder<FunctionSpec>();
            var diagnostics = ImmutableArray.CreateBuilder<DiagnosticSpec>();

            if (!IsPartial(classSymbol, cancellationToken))
            {
                diagnostics.Add(CreateDiagnosticSpec(DiagnosticIds.ClassMustBePartial, classSymbol, classSymbol.Name));
            }

            var implementsIHandleMessages = classSymbol.AllInterfaces
                .Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, knownTypes.IHandleMessages));

            if (implementsIHandleMessages)
            {
                diagnostics.Add(CreateDiagnosticSpec(DiagnosticIds.ShouldNotImplementIHandleMessages, classSymbol, classSymbol.Name));
            }

            foreach (var member in classSymbol.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is IMethodSymbol method)
                {
                    var spec = TryExtractFunctionSpec(method, knownTypes, diagnostics);
                    if (spec is not null)
                    {
                        functions.Add(spec);
                    }
                }
            }

            return new FunctionSpecs(functions.ToImmutableEquatableArray(), diagnostics.ToImmutableEquatableArray());
        }

        static FunctionSpecs ExtractFromMethod(IMethodSymbol methodSymbol, FunctionEndpointGeneratorKnownTypes knownTypes, CancellationToken cancellationToken)
        {
            var diagnostics = ImmutableArray.CreateBuilder<DiagnosticSpec>();

            if (!methodSymbol.IsPartialDefinition)
            {
                diagnostics.Add(CreateDiagnosticSpec(DiagnosticIds.MethodMustBePartial, methodSymbol, methodSymbol.Name));
            }

            if (!IsPartial(methodSymbol.ContainingType, cancellationToken))
            {
                diagnostics.Add(CreateDiagnosticSpec(DiagnosticIds.ClassMustBePartial, methodSymbol.ContainingType, methodSymbol.ContainingType.Name));
            }

            var spec = TryExtractFunctionSpec(methodSymbol, knownTypes, diagnostics);
            var functions = spec is not null
                ? ImmutableArray.Create(spec).ToImmutableEquatableArray()
                : ImmutableEquatableArray<FunctionSpec>.Empty;

            return new FunctionSpecs(functions, diagnostics.ToImmutable().ToImmutableEquatableArray());
        }

        static FunctionSpec? TryExtractFunctionSpec(IMethodSymbol method, FunctionEndpointGeneratorKnownTypes knownTypes, ImmutableArray<DiagnosticSpec>.Builder diagnostics)
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

            var configureMethod = GetConfigureMethodSpec(containingType, functionName, knownTypes, diagnostics);
            if (configureMethod is null)
            {
                return null;
            }

            return new FunctionSpec(
                ns, className, accessibility, method.Name, returnType,
                paramList.ToString(), messageParamName, functionContextParamName,
                cancellationTokenParamName, functionName, queueName, connectionName,
                configureMethod.Value);
        }

        static ConfigureMethodSpec? GetConfigureMethodSpec(INamedTypeSymbol functionClassType, string endpointName, FunctionEndpointGeneratorKnownTypes knownTypes, ImmutableArray<DiagnosticSpec>.Builder diagnostics)
        {
            var configureMethodName = $"Configure{endpointName}";

            IMethodSymbol? configureMethod = null;
            foreach (var member in functionClassType.GetMembers())
            {
                if (member is IMethodSymbol method && method.Name == configureMethodName)
                {
                    if (configureMethod is not null)
                    {
                        diagnostics.Add(CreateDiagnosticSpec(DiagnosticIds.MultipleConfigureMethods, functionClassType, configureMethodName, functionClassType.Name));
                        return null;
                    }

                    configureMethod = method;
                }
            }

            if (configureMethod is null)
            {
                return null;
            }

            var parameters = configureMethod.Parameters;

            if (parameters.Length == 0 || !SymbolEqualityComparer.Default.Equals(parameters[0].Type, knownTypes.EndpointConfiguration))
            {
                return null;
            }

            var optionalParameters = parameters.Skip(1);
            foreach (var parameter in optionalParameters)
            {
                var isAllowedOptionalParameter = SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IConfiguration) || SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IHostEnvironment);
                if (!isAllowedOptionalParameter)
                {
                    return null;
                }
            }

            var containingTypeFullyQualified = configureMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var parameterTypeNames = parameters.Select(p => p.Type.Name.ToLower()).ToImmutableEquatableArray();

            return new ConfigureMethodSpec(containingTypeFullyQualified, configureMethod.Name, parameterTypeNames);
        }

        static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
            => type.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax(cancellationToken) is ClassDeclarationSyntax classDeclaration
                && classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword));

        static DiagnosticSpec CreateDiagnosticSpec(string descriptorId, ISymbol symbol, params string[] arguments)
        {
            var location = symbol.Locations.FirstOrDefault();
            var filePath = location?.SourceTree?.FilePath ?? "";
            var span = location?.SourceSpan ?? default;
            var lineSpan = location?.GetLineSpan().Span ?? default;

            return new DiagnosticSpec(descriptorId, filePath, span, lineSpan, arguments.ToImmutableEquatableArray());
        }
    }

    internal readonly record struct ConfigureMethodSpec(string ContainingTypeFullyQualified, string MethodName, ImmutableEquatableArray<string> ParameterTypeNames);

    internal sealed record FunctionSpec(
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
        ConfigureMethodSpec ConfigureMethod);

    internal sealed record DiagnosticSpec(string DescriptorId, string FilePath, TextSpan Span, LinePositionSpan LineSpan, ImmutableEquatableArray<string> Arguments);

    internal readonly record struct FunctionSpecs(ImmutableEquatableArray<FunctionSpec> Functions, ImmutableEquatableArray<DiagnosticSpec> Diagnostics)
    {
        public static FunctionSpecs Empty { get; } = new(ImmutableEquatableArray<FunctionSpec>.Empty, ImmutableEquatableArray<DiagnosticSpec>.Empty);
    }

    readonly struct FunctionEndpointGeneratorKnownTypes(
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol serviceBusTriggerAttribute,
        INamedTypeSymbol functionContext,
        INamedTypeSymbol cancellationToken,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol iHandleMessages,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iHostEnvironment)
    {
        public INamedTypeSymbol FunctionAttribute { get; } = functionAttribute;
        public INamedTypeSymbol ServiceBusTriggerAttribute { get; } = serviceBusTriggerAttribute;
        public INamedTypeSymbol FunctionContext { get; } = functionContext;
        public INamedTypeSymbol CancellationToken { get; } = cancellationToken;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IHandleMessages { get; } = iHandleMessages;
        public INamedTypeSymbol IConfiguration { get; } = iConfiguration;
        public INamedTypeSymbol IHostEnvironment { get; } = iHostEnvironment;

        public static bool TryGet(Compilation compilation, out FunctionEndpointGeneratorKnownTypes knownTypes)
        {
            var functionAttribute =
                compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionAttribute");
            var serviceBusTriggerAttribute =
                compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute");
            var functionContext = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionContext");
            var cancellationToken = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
            var endpointConfiguration = compilation.GetTypeByMetadataName("NServiceBus.EndpointConfiguration");
            var iHandleMessages = compilation.GetTypeByMetadataName("NServiceBus.IHandleMessages`1");
            var iconfiguration = compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            var iHostEnvironment = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostEnvironment");

            if (functionAttribute is null
                || serviceBusTriggerAttribute is null
                || functionContext is null
                || cancellationToken is null
                || endpointConfiguration is null
                || iHandleMessages is null
                || iconfiguration is null
                || iHostEnvironment is null)
            {
                knownTypes = default;
                return false;
            }

            knownTypes = new FunctionEndpointGeneratorKnownTypes(
                functionAttribute,
                serviceBusTriggerAttribute,
                functionContext,
                cancellationToken,
                endpointConfiguration,
                iHandleMessages,
                iconfiguration,
                iHostEnvironment);

            return true;
        }
    }
}