namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;
using Utility;

readonly record struct CompilationAssemblyDetails(string SimpleName, string Identity)
{
    public static CompilationAssemblyDetails FromAssembly(IAssemblySymbol assembly) => new(assembly.Name, assembly.Identity.GetDisplayName());

    const string FunctionNamePrefix = "GeneratedFunctionRegistrations_";
    const string SendOnlyNamePrefix = "GeneratedSendOnlyEndpointRegistrations_";

    public string ToGenerationClassName()
    {
        return ToClassName(FunctionNamePrefix);
    }

    public string ToSendOnlyGenerationClassName()
    {
        return ToClassName(SendOnlyNamePrefix);
    }

    string ToClassName(string prefix)
    {
        var hash = NonCryptographicHash.GetHash(Identity);
        return $"{prefix}{SimpleName.Replace('.', '_')}_{hash:x16}";
    }
}