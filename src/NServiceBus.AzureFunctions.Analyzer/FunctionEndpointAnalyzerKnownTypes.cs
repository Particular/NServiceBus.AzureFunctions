#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

public readonly struct FunctionEndpointAnalyzerKnownTypes
{
    public FunctionEndpointAnalyzerKnownTypes(
        INamedTypeSymbol nServiceBusFunctionAttribute,
        INamedTypeSymbol iHandleMessages)
    {
        NServiceBusFunctionAttribute = nServiceBusFunctionAttribute;
        IHandleMessages = iHandleMessages;
    }

    public INamedTypeSymbol NServiceBusFunctionAttribute { get; }
    public INamedTypeSymbol IHandleMessages { get; }

    public static bool TryGet(Compilation compilation, out FunctionEndpointAnalyzerKnownTypes knownTypes)
    {
        var nsbFunctionAttribute = compilation.GetTypeByMetadataName("NServiceBus.NServiceBusFunctionAttribute");
        var iHandleMessages = compilation.GetTypeByMetadataName("NServiceBus.IHandleMessages`1");

        if (nsbFunctionAttribute is null || iHandleMessages is null)
        {
            knownTypes = default;
            return false;
        }

        knownTypes = new FunctionEndpointAnalyzerKnownTypes(nsbFunctionAttribute, iHandleMessages);
        return true;
    }
}
