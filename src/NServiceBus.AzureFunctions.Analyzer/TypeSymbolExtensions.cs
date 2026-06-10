namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

static class TypeSymbolExtensions
{
    public const string AllowedConfigureParameterTypesDescription = "IServiceCollection, IConfigurationManager, or IHostEnvironment (or types they are assignable to)";

    extension(ITypeSymbol type)
    {
        public bool IsAllowedConfigureMethodParameterType(
            INamedTypeSymbol iServiceCollection,
            INamedTypeSymbol iConfigurationManager,
            INamedTypeSymbol iHostEnvironment)
            => IsAssignableTo(iServiceCollection, type)
               || IsAssignableTo(iConfigurationManager, type)
               || IsAssignableTo(iHostEnvironment, type);
    }

    static bool IsAssignableTo(INamedTypeSymbol sourceType, ITypeSymbol targetType)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
        {
            return true;
        }

        foreach (var iface in sourceType.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, targetType))
            {
                return true;
            }
        }

        var baseType = sourceType.BaseType;
        while (baseType is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, targetType))
            {
                return true;
            }
            baseType = baseType.BaseType;
        }

        return false;
    }

    public static string? TryResolveDelegateParameterName(ITypeSymbol userType, INamedTypeSymbol delegateType, string canonicalName)
        => IsAssignableTo(delegateType, userType) ? canonicalName : null;

    public static string ToCamelCaseParameterName(ITypeSymbol type)
    {
        var name = type.Name;
        var start = name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? 1 : 0;
        var length = name.Length - start;
        var chars = new char[length];
        chars[0] = char.ToLowerInvariant(name[start]);
        for (var i = 1; i < length; i++)
        {
            chars[i] = name[start + i];
        }
        return new string(chars);
    }
}