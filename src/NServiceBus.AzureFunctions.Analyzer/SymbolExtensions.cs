namespace NServiceBus.AzureFunctions.Analyzer;

using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

static class SymbolExtensions
{
    extension(ISymbol symbol)
    {
        public bool HasAttribute(INamedTypeSymbol attributeType)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetAttribute(INamedTypeSymbol attributeType, [NotNullWhen(true)] out AttributeData? attributeData)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    attributeData = attribute;
                    return true;
                }
            }

            attributeData = null;
            return false;
        }

        public Diagnostic CreateDiagnostic(DiagnosticDescriptor descriptor, params object[] arguments)
        {
            var location = symbol.Locations.Length > 0 ? symbol.Locations[0] : null;
            return Diagnostic.Create(descriptor, location, arguments);
        }
    }
}