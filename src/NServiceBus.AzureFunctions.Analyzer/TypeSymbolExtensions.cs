namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

static class TypeSymbolExtensions
{
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
}