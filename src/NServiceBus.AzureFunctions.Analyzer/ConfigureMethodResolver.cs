namespace NServiceBus.AzureFunctions.Analyzer;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using NServiceBus.Core.Analyzer;

internal readonly record struct ConfigureMethodSpec(string ContainingTypeFullyQualified, string MethodName, ImmutableEquatableArray<string> ParameterTypeNames, ImmutableEquatableArray<string> DelegateParameterNames);

internal readonly record struct ConfigureMethodResolution(ConfigureMethodSpec? Spec, ImmutableEquatableArray<string> Problems)
{
    public bool IsSuccess => Spec is not null;
}

static class ConfigureMethodResolver
{
    public static ConfigureMethodResolution Resolve(
        IMethodSymbol method,
        INamedTypeSymbol endpointConfigurationType,
        INamedTypeSymbol delegateType)
    {
        var problems = new List<string>();

        if (method.Parameters.Length == 0 || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, endpointConfigurationType))
        {
            problems.Add("first parameter must be EndpointConfiguration");
        }

        var delegateParameters = delegateType.DelegateInvokeMethod?.Parameters;
        if (delegateParameters is null)
        {
            return new ConfigureMethodResolution(null, problems.ToImmutableEquatableArray());
        }

        for (var i = 1; i < method.Parameters.Length; i++)
        {
            var matched = false;
            for (var j = 1; j < delegateParameters.Value.Length; j++)
            {
                if (method.Parameters[i].Type.IsAssignableToDelegateParameter((INamedTypeSymbol)delegateParameters.Value[j].Type))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                var allowedTypes = string.Join(", ", delegateParameters.Value.Skip(1).Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                problems.Add($"parameters after EndpointConfiguration must be compatible with: {allowedTypes}");
                break;
            }
        }

        if (problems.Count > 0)
        {
            return new ConfigureMethodResolution(null, problems.ToImmutableEquatableArray());
        }

        var delegateParamNames = new string[delegateParameters.Value.Length];
        for (var i = 0; i < delegateParameters.Value.Length; i++)
        {
            delegateParamNames[i] = TypeSymbolExtensions.ToCamelCaseParameterName(delegateParameters.Value[i].Type);
        }

        var parameterTypeNames = new string[method.Parameters.Length];
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i == 0)
            {
                parameterTypeNames[i] = delegateParamNames[0];
                continue;
            }

            var resolved = false;
            for (var j = 1; j < delegateParameters.Value.Length; j++)
            {
                if (method.Parameters[i].Type.IsAssignableToDelegateParameter((INamedTypeSymbol)delegateParameters.Value[j].Type))
                {
                    parameterTypeNames[i] = delegateParamNames[j];
                    resolved = true;
                    break;
                }
            }

            if (!resolved)
            {
                parameterTypeNames[i] = TypeSymbolExtensions.ToCamelCaseParameterName(method.Parameters[i].Type);
            }
        }

        var spec = new ConfigureMethodSpec(
            method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            method.Name,
            parameterTypeNames.ToImmutableEquatableArray(),
            delegateParamNames.ToImmutableEquatableArray());

        return new ConfigureMethodResolution(spec, ImmutableEquatableArray<string>.Empty);
    }
}