namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

static class TypeSymbolExtensions
{
    public const string AllowedConfigureParameterTypesDescription = "IServiceCollection, IConfigurationManager, IConfiguration, IConfigurationBuilder, or IHostEnvironment";

    extension(ITypeSymbol type)
    {
        public bool IsAllowedConfigureMethodParameterType(
            INamedTypeSymbol iServiceCollection,
            INamedTypeSymbol iConfigurationManager,
            INamedTypeSymbol iConfiguration,
            INamedTypeSymbol iConfigurationBuilder,
            INamedTypeSymbol iHostEnvironment)
            => SymbolEqualityComparer.Default.Equals(type, iServiceCollection)
               || SymbolEqualityComparer.Default.Equals(type, iConfigurationManager)
               || SymbolEqualityComparer.Default.Equals(type, iConfiguration)
               || SymbolEqualityComparer.Default.Equals(type, iConfigurationBuilder)
               || SymbolEqualityComparer.Default.Equals(type, iHostEnvironment);
    }

    /// <summary>
    /// Converts a type name to a camelCase parameter name.
    /// Interface names following the I+PascalCase convention have the I prefix stripped.
    /// </summary>
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