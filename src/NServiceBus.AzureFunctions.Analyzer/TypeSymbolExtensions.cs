namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

static class TypeSymbolExtensions
{
    extension(ITypeSymbol type)
    {
        public bool IsAssignableToDelegateParameter(INamedTypeSymbol delegateType)
        {
            if (SymbolEqualityComparer.Default.Equals(type, delegateType))
            {
                return true;
            }

            foreach (var iface in delegateType.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, type))
                {
                    return true;
                }
            }

            var baseType = delegateType.BaseType;
            while (baseType is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(baseType, type))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }

            return false;
        }

        public string ToCamelCaseParameterName()
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
}