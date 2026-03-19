namespace NServiceBus.AzureFunctions.Analyzer;

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading;
using NServiceBus.Core.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            var functions = new List<FunctionSpec>();
            var diagnostics = new List<Diagnostic>();

            if (!IsPartial(classSymbol, cancellationToken))
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ClassMustBePartialDescriptor, classSymbol, classSymbol.Name));
            }

            if (ImplementsIHandleMessages(classSymbol, knownTypes))
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ShouldNotImplementIHandleMessagesDescriptor, classSymbol, classSymbol.Name));
            }

            foreach (var member in classSymbol.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (member is IMethodSymbol method)
                {
                    var spec = ExtractFunctionSpec(method, knownTypes, diagnostics);
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
            var diagnostics = new List<Diagnostic>();

            if (!methodSymbol.IsPartialDefinition)
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.MethodMustBePartialDescriptor, methodSymbol, methodSymbol.Name));
            }

            if (!IsPartial(methodSymbol.ContainingType, cancellationToken))
            {
                diagnostics.Add(CreateDiagnostic(DiagnosticIds.ClassMustBePartialDescriptor, methodSymbol.ContainingType, methodSymbol.ContainingType.Name));
            }

            var spec = ExtractFunctionSpec(methodSymbol, knownTypes, diagnostics);
            var functions = spec is null ? ImmutableEquatableArray<FunctionSpec>.Empty : ((FunctionSpec[])[spec]).ToImmutableEquatableArray();
            return new FunctionSpecs(functions, diagnostics.ToImmutableEquatableArray());
        }

        static FunctionSpec? ExtractFunctionSpec(IMethodSymbol method, FunctionEndpointGeneratorKnownTypes knownTypes, List<Diagnostic> diagnostics)
        {
            if (!TryGetFunctionAttribute(method, knownTypes, out var functionAttr) || functionAttr.ConstructorArguments.Length == 0)
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
            string? messageActionsParamName = null;

            var paramList = new StringBuilder();

            for (var i = 0; i < method.Parameters.Length; i++)
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

                if (SymbolEqualityComparer.Default.Equals(param.Type, knownTypes.MessageActions))
                {
                    messageActionsParamName = param.Name;
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

            if (queueName is null || functionContextParamName is null || messageParamName is null || messageActionsParamName is null)
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
                paramList.ToString(), messageParamName, messageActionsParamName, functionContextParamName,
                cancellationTokenParamName, functionName, queueName, connectionName,
                configureMethod.Value);
        }

        static ConfigureMethodSpec? GetConfigureMethodSpec(INamedTypeSymbol functionClassType, string endpointName, FunctionEndpointGeneratorKnownTypes knownTypes, List<Diagnostic> diagnostics)
        {
            var configureMethodName = $"Configure{endpointName}";

            IMethodSymbol? configureMethod = null;
            foreach (var member in functionClassType.GetMembers())
            {
                if (member is IMethodSymbol method && method.Name == configureMethodName)
                {
                    if (configureMethod is not null)
                    {
                        diagnostics.Add(CreateDiagnostic(DiagnosticIds.MultipleConfigureMethodsDescriptor, functionClassType, configureMethodName, functionClassType.Name));
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

            for (var i = 1; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var isAllowedOptionalParameter = SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IConfiguration) || SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.IHostEnvironment);
                if (!isAllowedOptionalParameter)
                {
                    return null;
                }
            }

            var containingTypeFullyQualified = configureMethod.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var parameterTypeNames = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                parameterTypeNames[i] = parameters[i].Type.Name.ToLowerInvariant();
            }

            return new ConfigureMethodSpec(containingTypeFullyQualified, configureMethod.Name, parameterTypeNames.ToImmutableEquatableArray());
        }

        static bool ImplementsIHandleMessages(INamedTypeSymbol classSymbol, FunctionEndpointGeneratorKnownTypes knownTypes)
        {
            foreach (var @interface in classSymbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, knownTypes.IHandleMessages))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryGetFunctionAttribute(IMethodSymbol method, FunctionEndpointGeneratorKnownTypes knownTypes, [NotNullWhen(true)] out AttributeData? functionAttribute)
        {
            foreach (var attribute in method.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownTypes.FunctionAttribute))
                {
                    functionAttribute = attribute;
                    return true;
                }
            }

            functionAttribute = null!;
            return false;
        }

        static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken)
        {
            foreach (var syntaxReference in type.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(cancellationToken) is not ClassDeclarationSyntax classDeclaration)
                {
                    continue;
                }

                var modifiers = classDeclaration.Modifiers;
                for (var i = 0; i < modifiers.Count; i++)
                {
                    if (modifiers[i].IsKind(SyntaxKind.PartialKeyword))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        static Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, ISymbol symbol, params object[] arguments)
        {
            var locations = symbol.Locations;
            var location = locations.Length > 0 ? locations[0] : null;
            return Diagnostic.Create(descriptor, location, arguments);
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
        string MessageActionsParamName,
        string FunctionContextParamName,
        string CancellationTokenParamName,
        string FunctionName,
        string QueueName,
        string ConnectionName,
        ConfigureMethodSpec ConfigureMethod);

    internal readonly record struct FunctionSpecs(ImmutableEquatableArray<FunctionSpec> Functions, ImmutableEquatableArray<Diagnostic> Diagnostics)
    {
        public static FunctionSpecs Empty { get; } = new(ImmutableEquatableArray<FunctionSpec>.Empty, ImmutableEquatableArray<Diagnostic>.Empty);
    }

    readonly struct FunctionEndpointGeneratorKnownTypes(
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol serviceBusTriggerAttribute,
        INamedTypeSymbol functionContext,
        INamedTypeSymbol cancellationToken,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol iHandleMessages,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iHostEnvironment,
        INamedTypeSymbol messageActions)
    {
        public INamedTypeSymbol FunctionAttribute { get; } = functionAttribute;
        public INamedTypeSymbol ServiceBusTriggerAttribute { get; } = serviceBusTriggerAttribute;
        public INamedTypeSymbol FunctionContext { get; } = functionContext;
        public INamedTypeSymbol CancellationToken { get; } = cancellationToken;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IHandleMessages { get; } = iHandleMessages;
        public INamedTypeSymbol IConfiguration { get; } = iConfiguration;
        public INamedTypeSymbol IHostEnvironment { get; } = iHostEnvironment;
        public INamedTypeSymbol MessageActions { get; } = messageActions;

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
            var messageActions = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.ServiceBusMessageActions");
            if (functionAttribute is null
                || serviceBusTriggerAttribute is null
                || functionContext is null
                || cancellationToken is null
                || endpointConfiguration is null
                || iHandleMessages is null
                || iconfiguration is null
                || iHostEnvironment is null
                || messageActions is null)
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
                iHostEnvironment,
                messageActions);

            return true;
        }
    }
}