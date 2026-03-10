#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using NServiceBus.AzureFunctions.Analyzer.Utility;

record struct CompilationAssemblyDetails(string SimpleName, string Identity)
{
    public static CompilationAssemblyDetails FromAssembly(IAssemblySymbol assembly) => new(assembly.Name, assembly.Identity.GetDisplayName());

    const string NamePrefix = "GeneratedFunctionRegistrations_";
    public readonly string ToGenerationClassName()
    {
        var hash = NonCryptographicHash.GetHash(Identity);
        return $"{NamePrefix}{SimpleName.Replace('.', '_')}_{hash:x16}";
    }
}