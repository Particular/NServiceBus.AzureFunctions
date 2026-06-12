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
                IMethodSymbol methodSymbol => ExtractFromMethod(methodSymbol, knownTypes, triggerDefinition, cancellationToken),
                _ => FunctionSpecs.Empty
            };
        }

        static FunctionSpecs ExtractFromMethod(IMethodSymbol methodSymbol, FunctionEndpointGeneratorKnownTypes knownTypes, TriggerDefinition triggerDefinition, CancellationToken cancellationToken)
        {
            var diagnostics = new List<Diagnostic>();

            if (!methodSymbol.IsPartialDefinition)
            {
                diagnostics.Add(methodSymbol.CreateDiagnostic(DiagnosticIds.MethodMustBePartialDescriptor, methodSymbol.Name));
            }

            var classSymbol = methodSymbol.ContainingType;

            if (!IsPartial(classSymbol, cancellationToken))
            {
                diagnostics.Add(classSymbol.CreateDiagnostic(DiagnosticIds.ClassMustBePartialDescriptor, classSymbol.Name));
            }

            if (ImplementsIHandleMessages(classSymbol, knownTypes))
            {
                diagnostics.Add(classSymbol.CreateDiagnostic(DiagnosticIds.ShouldNotImplementIHandleMessagesDescriptor, classSymbol.Name));
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
            string? autoCompletePropertyName = null;
            var autoCompleteMustBeDisabled = false;
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

                        if (TryExtractAddress(pAttr, triggerDefinition.AddressExtraction, out var extractedAddress))
                        {
                            addressName = extractedAddress;
                        }

                        if (TryExtractConnectionSetting(pAttr, triggerDefinition.ConnectionSetting, out var extractedConnectionSetting))
                        {
                            connectionSettingName = extractedConnectionSetting;
                        }

                        if (triggerDefinition.AutoComplete is AutoCompletePolicy.MustBeFalse autoCompletePolicy
                            && IsAutoCompleteEnabled(pAttr, autoCompletePolicy))
                        {
                            autoCompleteMustBeDisabled = true;
                            autoCompletePropertyName ??= autoCompletePolicy.PropertyName;
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
            var configureMethodName = KnownTypeNames.ConfigureMethodName(functionName);
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

            if (autoCompleteMustBeDisabled)
            {
                diagnostics.Add(
                    method.CreateDiagnostic(
                        DiagnosticIds.AutoCompleteMustBeExplicitlyDisabled,
                        method.Name,
                        autoCompletePropertyName!,
                        knownTypes.TriggerAttribute.Name));
            }

            connectionSettingName ??= "";

            var ns = containingType.ContainingNamespace.IsGlobalNamespace ? "" : containingType.ContainingNamespace.ToDisplayString();
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

        static bool TryExtractAddress(AttributeData triggerAttribute, AddressExtractionPolicy policy, [NotNullWhen(true)] out string? address)
        {
            switch (policy)
            {
                case AddressExtractionPolicy.ConstructorArgument(var index)
                    when triggerAttribute.ConstructorArguments.Length > index:
                    address = triggerAttribute.ConstructorArguments[index].Value as string;
                    return address is not null;

                case AddressExtractionPolicy.ConstructorArgument:
                    address = null;
                    return false;

                case AddressExtractionPolicy.NamedProperty(var propertyName):
                    return TryGetNamedArgumentString(triggerAttribute, propertyName, out address);

                case AddressExtractionPolicy.ConstructorParameterNamed(var parameterName):
                    return TryGetConstructorArgumentStringByParameterName(triggerAttribute, parameterName, out address);

                default:
                    throw new InvalidOperationException($"Unsupported address extraction policy: {policy.GetType().Name}.");
            }
        }

        static bool TryExtractConnectionSetting(AttributeData triggerAttribute, ConnectionSettingPolicy policy, [NotNullWhen(true)] out string? connectionSetting)
        {
            switch (policy)
            {
                case ConnectionSettingPolicy.NamedProperty(var propertyName):
                    return TryGetNamedArgumentString(triggerAttribute, propertyName, out connectionSetting);

                case ConnectionSettingPolicy.None:
                    connectionSetting = null;
                    return false;

                default:
                    throw new InvalidOperationException($"Unsupported connection setting policy: {policy.GetType().Name}.");
            }
        }

        static bool IsAutoCompleteEnabled(AttributeData triggerAttribute, AutoCompletePolicy.MustBeFalse policy)
        {
            foreach (var namedArg in triggerAttribute.NamedArguments)
            {
                if (namedArg.Key == policy.PropertyName)
                {
                    return namedArg.Value.Value as bool? ?? true;
                }
            }

            return true;
        }

        static bool TryGetNamedArgumentString(AttributeData triggerAttribute, string propertyName, [NotNullWhen(true)] out string? value)
        {
            foreach (var namedArg in triggerAttribute.NamedArguments)
            {
                if (namedArg.Key == propertyName)
                {
                    value = namedArg.Value.Value as string;
                    return value is not null;
                }
            }

            value = null;
            return false;
        }

        static bool TryGetConstructorArgumentStringByParameterName(
            AttributeData triggerAttribute,
            string parameterName,
            [NotNullWhen(true)] out string? value)
        {
            var constructor = triggerAttribute.AttributeConstructor;
            if (constructor is null)
            {
                value = null;
                return false;
            }

            var parameters = constructor.Parameters;
            var arguments = triggerAttribute.ConstructorArguments;
            var argumentCount = Math.Min(parameters.Length, arguments.Length);

            for (var i = 0; i < argumentCount; i++)
            {
                if (parameters[i].Name == parameterName)
                {
                    value = arguments[i].Value as string;
                    return value is not null;
                }
            }

            value = null;
            return false;
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
            var configureMethodName = KnownTypeNames.ConfigureMethodName(endpointName);

            IMethodSymbol? configureMethod = null;
            foreach (var member in functionClassType.GetMembers())
            {
                if (member is IMethodSymbol method && string.Equals(method.Name, configureMethodName, StringComparison.OrdinalIgnoreCase))
                {
                    if (configureMethod is not null)
                    {
                        diagnostics.Add(functionClassType.CreateDiagnostic(DiagnosticIds.MultipleConfigureMethodsDescriptor, configureMethodName, functionClassType.Name));
                        return null;
                    }

                    configureMethod = method;
                }
            }

            if (configureMethod is null)
            {
                return null;
            }

            var resolution = ConfigureMethodResolver.Resolve(configureMethod, knownTypes.EndpointConfiguration, knownTypes.DelegateType);
            return resolution.Spec;
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
            => method.TryGetAttribute(knownTypes.FunctionAttribute, out functionAttribute);

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
    }

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

    internal readonly record struct FunctionSpecs(ImmutableEquatableArray<FunctionSpec> Functions, ImmutableEquatableArray<Diagnostic> Diagnostics) : IDiagnosticsSpec
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
        INamedTypeSymbol delegateType,
        ImmutableDictionary<ParameterRole, INamedTypeSymbol> additionalParameterSymbols)
    {
        public INamedTypeSymbol FunctionAttribute { get; } = functionAttribute;
        public INamedTypeSymbol TriggerAttribute { get; } = triggerAttribute;
        public INamedTypeSymbol FunctionContext { get; } = functionContext;
        public INamedTypeSymbol CancellationToken { get; } = cancellationToken;
        public INamedTypeSymbol EndpointConfiguration { get; } = endpointConfiguration;
        public INamedTypeSymbol IHandleMessages { get; } = iHandleMessages;
        public INamedTypeSymbol DelegateType { get; } = delegateType;
        public ImmutableDictionary<ParameterRole, INamedTypeSymbol> AdditionalParameterSymbols { get; } = additionalParameterSymbols;

        public static bool TryGet(Compilation compilation, TriggerDefinition triggerDefinition, out FunctionEndpointGeneratorKnownTypes knownTypes)
        {
            var functionAttribute =
                compilation.GetTypeByMetadataName(KnownTypeNames.FunctionAttribute);
            var triggerAttribute =
                compilation.GetTypeByMetadataName(triggerDefinition.TriggerAttributeMetadataName);
            var functionContext = compilation.GetTypeByMetadataName(KnownTypeNames.FunctionContext);
            var cancellationToken = compilation.GetTypeByMetadataName(KnownTypeNames.CancellationToken);
            var endpointConfiguration = compilation.GetTypeByMetadataName(KnownTypeNames.EndpointConfigurationType);
            var iHandleMessages = compilation.GetTypeByMetadataName(KnownTypeNames.IHandleMessages);
            var delegateType = compilation.GetTypeByMetadataName(KnownTypeNames.FunctionEndpointConfiguration);

            if (functionAttribute is null
                || triggerAttribute is null
                || functionContext is null
                || cancellationToken is null
                || endpointConfiguration is null
                || iHandleMessages is null
                || delegateType is null)
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
                delegateType,
                additionalBuilder.ToImmutable());

            return true;
        }
    }
}