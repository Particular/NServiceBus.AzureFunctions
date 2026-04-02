namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
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
        internal static FunctionSpecs Extract(GeneratorAttributeSyntaxContext context, TriggerDefinition triggerDefinition, CancellationToken cancellationToken = default)
        {
            if (context.Attributes.Length == 0)
            {
                return FunctionSpecs.Empty;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!FunctionEndpointGeneratorKnownTypes.TryGet(context.SemanticModel.Compilation, triggerDefinition, out var knownTypes))
            {
                return FunctionSpecs.Empty;
            }

            return context.TargetSymbol switch
            {
                INamedTypeSymbol classSymbol => ExtractFromClass(classSymbol, knownTypes, triggerDefinition, cancellationToken),
                IMethodSymbol methodSymbol => ExtractFromMethod(methodSymbol, knownTypes, triggerDefinition, cancellationToken),
                _ => FunctionSpecs.Empty
            };
        }

        static FunctionSpecs ExtractFromClass(INamedTypeSymbol classSymbol, FunctionEndpointGeneratorKnownTypes knownTypes, TriggerDefinition triggerDefinition, CancellationToken cancellationToken)
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
                    var spec = ExtractFunctionSpec(method, knownTypes, triggerDefinition, diagnostics);
                    if (spec is not null)
                    {
                        functions.Add(spec);
                    }
                }
            }

            return new FunctionSpecs(functions.ToImmutableEquatableArray(), diagnostics.ToImmutableEquatableArray());
        }

        static FunctionSpecs ExtractFromMethod(IMethodSymbol methodSymbol, FunctionEndpointGeneratorKnownTypes knownTypes, TriggerDefinition triggerDefinition, CancellationToken cancellationToken)
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

            var spec = ExtractFunctionSpec(methodSymbol, knownTypes, triggerDefinition, diagnostics);
            var functions = spec is null ? ImmutableEquatableArray<FunctionSpec>.Empty : ((FunctionSpec[])[spec]).ToImmutableEquatableArray();
            return new FunctionSpecs(functions, diagnostics.ToImmutableEquatableArray());
        }

        static FunctionSpec? ExtractFunctionSpec(IMethodSymbol method, FunctionEndpointGeneratorKnownTypes knownTypes, TriggerDefinition triggerDefinition, List<Diagnostic> diagnostics)
        {
            if (!TryGetFunctionAttribute(method, knownTypes, out var functionAttr) || functionAttr.ConstructorArguments.Length == 0)
            {
                return null;
            }

            if (functionAttr.ConstructorArguments[0].Value is not string functionName)
            {
                return null;
            }

            string? addressName = null;
            string? connectionSettingName = null;
            string? messageParamName = null;
            string? functionContextParamName = null;
            string? cancellationTokenParamName = null;
            var additionalParamNames = new Dictionary<ParameterRole, string>();
            var parameterRoles = new List<ParameterRole?>();
            var triggerParameterCount = 0;

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

                var hasTriggerAttribute = false;
                foreach (var pAttr in param.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(pAttr.AttributeClass, knownTypes.TriggerAttribute))
                    {
                        hasTriggerAttribute = true;
                        triggerParameterCount++;

                        if (triggerParameterCount == 1)
                        {
                            messageParamName = param.Name;
                        }

                        if (pAttr.ConstructorArguments.Length > 0)
                        {
                            addressName = pAttr.ConstructorArguments[0].Value as string;
                        }

                        var autoCompleteEnabled = triggerDefinition.RequireAutoCompleteFalse;
                        foreach (var namedArg in pAttr.NamedArguments)
                        {
                            if (triggerDefinition.ConnectionPropertyName is not null
                                && namedArg.Key == triggerDefinition.ConnectionPropertyName)
                            {
                                connectionSettingName = namedArg.Value.Value as string;
                            }

                            if (triggerDefinition.RequireAutoCompleteFalse
                                && triggerDefinition.AutoCompletePropertyName is not null
                                && namedArg.Key == triggerDefinition.AutoCompletePropertyName)
                            {
                                var autoComplete = namedArg.Value.Value as bool?;
                                autoCompleteEnabled = autoComplete!.Value;
                            }
                        }

                        if (triggerDefinition.RequireAutoCompleteFalse && autoCompleteEnabled)
                        {
                            diagnostics.Add(CreateDiagnostic(DiagnosticIds.AutoCompleteMustBeExplicitlyDisabled, method, method.Name));
                        }
                    }
                }

                var role = ClassifyParameterRole(param, hasTriggerAttribute, knownTypes);
                parameterRoles.Add(role);

                if (role is not null && knownTypes.AdditionalParameterSymbols.ContainsKey(role.Value) && !additionalParamNames.ContainsKey(role.Value))
                {
                    additionalParamNames[role.Value] = param.Name;
                }

                if (role == ParameterRole.FunctionContext && functionContextParamName is null)
                {
                    functionContextParamName = param.Name;
                }

                if (role == ParameterRole.CancellationToken && cancellationTokenParamName is null)
                {
                    cancellationTokenParamName = param.Name;
                }
            }

            var problems = ImmutableList.CreateBuilder<string>();

            if (triggerParameterCount == 0)
            {
                problems.Add("missing a parameter with a trigger attribute");
            }
            else if (triggerParameterCount > 1)
            {
                problems.Add("must declare exactly one trigger parameter");
            }

            if (messageParamName is not null && addressName is null)
            {
                problems.Add("trigger attribute does not specify an address or entity name");
            }

            if (functionContextParamName is null)
            {
                problems.Add("missing FunctionContext parameter");
            }

            foreach (var kvp in knownTypes.AdditionalParameterSymbols)
            {
                if (!additionalParamNames.ContainsKey(kvp.Key))
                {
                    problems.Add($"missing {kvp.Key.Name} parameter");
                }
            }

            if (cancellationTokenParamName is null)
            {
                problems.Add("missing CancellationToken parameter");
            }

            if (!MatchesShape(parameterRoles, triggerDefinition.Shape))
            {
                problems.Add($"parameters must match required shape {FormatShape(triggerDefinition.Shape.OrderedParameters)}");
            }

            var containingType = method.ContainingType;
            var configureMethodName = $"Configure{functionName}";
            var configureMethod = GetConfigureMethodSpec(containingType, functionName, knownTypes, diagnostics);
            if (configureMethod is null)
            {
                problems.Add($"missing '{configureMethodName}' configuration method");
            }

            if (problems.Count > 0)
            {
                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                properties.Add("ConfigureMethodName", configureMethodName);
                if (functionContextParamName is null)
                {
                    properties.Add("MissingFunctionContext", "true");
                }
                if (cancellationTokenParamName is null)
                {
                    properties.Add("MissingCancellationToken", "true");
                }
                foreach (var kvp in knownTypes.AdditionalParameterSymbols)
                {
                    if (!additionalParamNames.ContainsKey(kvp.Key))
                    {
                        properties.Add($"Missing{kvp.Key.Name}", kvp.Value.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                    }
                }
                if (configureMethod is null)
                {
                    properties.Add("MissingConfigureMethod", "true");
                }
                var location = method.Locations.Length > 0 ? method.Locations[0] : null;
                diagnostics.Add(Diagnostic.Create(DiagnosticIds.InvalidFunctionMethodDescriptor, location, properties.ToImmutable(), method.Name, string.Join(", ", problems)));
                return null;
            }

            connectionSettingName ??= "";

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

            var processArgParts = new List<string>();
            foreach (var role in triggerDefinition.Shape.OrderedParameters)
            {
                if (role == ParameterRole.TriggerMessage)
                {
                    processArgParts.Add(messageParamName!);
                }
                else if (role == ParameterRole.FunctionContext)
                {
                    processArgParts.Add(functionContextParamName!);
                }
                else if (role == ParameterRole.CancellationToken)
                {
                    processArgParts.Add(cancellationTokenParamName!);
                }
                else if (additionalParamNames.TryGetValue(role, out var name))
                {
                    processArgParts.Add(name);
                }
            }
            var processArgs = string.Join(", ", processArgParts);
            var processCallExpression = $"processor.{triggerDefinition.ProcessMethodName}({processArgs})";

            return new FunctionSpec(
                ns, className, accessibility, method.Name, returnType,
                paramList.ToString(), functionContextParamName!,
                functionName, addressName!, connectionSettingName,
                triggerDefinition.ProcessorTypeFullyQualified, triggerDefinition.RegistrationMethodFullyQualified, processCallExpression, configureMethod!.Value);
        }

        static ParameterRole? ClassifyParameterRole(IParameterSymbol parameter, bool hasTriggerAttribute, FunctionEndpointGeneratorKnownTypes knownTypes)
        {
            if (hasTriggerAttribute)
            {
                return ParameterRole.TriggerMessage;
            }

            foreach (var kvp in knownTypes.AdditionalParameterSymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, kvp.Value))
                {
                    return kvp.Key;
                }
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.FunctionContext))
            {
                return ParameterRole.FunctionContext;
            }

            if (SymbolEqualityComparer.Default.Equals(parameter.Type, knownTypes.CancellationToken))
            {
                return ParameterRole.CancellationToken;
            }

            return null;
        }

        static bool MatchesShape(List<ParameterRole?> actualParameterRoles, TriggerShape shape)
        {
            if (!shape.AllowAdditionalParameters)
            {
                if (actualParameterRoles.Count != shape.OrderedParameters.Count)
                {
                    return false;
                }

                for (var i = 0; i < actualParameterRoles.Count; i++)
                {
                    if (actualParameterRoles[i] is not { } role || role != shape.OrderedParameters[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            var roleIndex = 0;
            for (var i = 0; i < actualParameterRoles.Count; i++)
            {
                if (actualParameterRoles[i] is not { } role)
                {
                    continue;
                }

                if (roleIndex >= shape.OrderedParameters.Count || role != shape.OrderedParameters[roleIndex])
                {
                    return false;
                }

                roleIndex++;
            }

            return roleIndex == shape.OrderedParameters.Count;
        }

        static string FormatShape(ImmutableEquatableArray<ParameterRole> orderedParameters)
        {
            var builder = new StringBuilder("[");

            for (var i = 0; i < orderedParameters.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(orderedParameters[i]);
            }

            builder.Append(']');
            return builder.ToString();
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
        string FunctionContextParamName,
        string FunctionName,
        string AddressName,
        string ConnectionSettingName,
        string ProcessorTypeFullyQualified,
        string RegistrationMethodFullyQualified,
        string ProcessCallExpression,
        ConfigureMethodSpec ConfigureMethod);

    internal readonly record struct FunctionSpecs(ImmutableEquatableArray<FunctionSpec> Functions, ImmutableEquatableArray<Diagnostic> Diagnostics)
    {
        public static FunctionSpecs Empty { get; } = new(ImmutableEquatableArray<FunctionSpec>.Empty, ImmutableEquatableArray<Diagnostic>.Empty);
    }

    readonly struct FunctionEndpointGeneratorKnownTypes(
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol triggerAttribute,
        INamedTypeSymbol functionContext,
        INamedTypeSymbol cancellationToken,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol iHandleMessages,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iHostEnvironment,
        ImmutableDictionary<ParameterRole, INamedTypeSymbol> additionalParameterSymbols)
    {
        public INamedTypeSymbol FunctionAttribute { get; } = functionAttribute;
        public INamedTypeSymbol TriggerAttribute { get; } = triggerAttribute;
        public INamedTypeSymbol FunctionContext { get; } = functionContext;
        public INamedTypeSymbol CancellationToken { get; } = cancellationToken;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IHandleMessages { get; } = iHandleMessages;
        public INamedTypeSymbol IConfiguration { get; } = iConfiguration;
        public INamedTypeSymbol IHostEnvironment { get; } = iHostEnvironment;
        public ImmutableDictionary<ParameterRole, INamedTypeSymbol> AdditionalParameterSymbols { get; } = additionalParameterSymbols;

        public static bool TryGet(Compilation compilation, TriggerDefinition triggerDefinition, out FunctionEndpointGeneratorKnownTypes knownTypes)
        {
            var functionAttribute =
                compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionAttribute");
            var triggerAttribute =
                compilation.GetTypeByMetadataName(triggerDefinition.TriggerAttributeMetadataName);
            var functionContext = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionContext");
            var cancellationToken = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
            var endpointConfiguration = compilation.GetTypeByMetadataName("NServiceBus.EndpointConfiguration");
            var iHandleMessages = compilation.GetTypeByMetadataName("NServiceBus.IHandleMessages`1");
            var iconfiguration = compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            var iHostEnvironment = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostEnvironment");

            if (functionAttribute is null
                || triggerAttribute is null
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

            var additionalBuilder = ImmutableDictionary.CreateBuilder<ParameterRole, INamedTypeSymbol>();
            foreach (var apt in triggerDefinition.AdditionalParameterTypes)
            {
                var symbol = compilation.GetTypeByMetadataName(apt.MetadataName);
                if (symbol is null)
                {
                    knownTypes = default;
                    return false;
                }
                additionalBuilder.Add(apt.Role, symbol);
            }

            knownTypes = new FunctionEndpointGeneratorKnownTypes(
                functionAttribute,
                triggerAttribute,
                functionContext,
                cancellationToken,
                endpointConfiguration,
                iHandleMessages,
                iconfiguration,
                iHostEnvironment,
                additionalBuilder.ToImmutable());

            return true;
        }
    }
}

