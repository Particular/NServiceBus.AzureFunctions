#nullable enable
namespace NServiceBus.AzureFunctions.Analyzer;

using Microsoft.CodeAnalysis;

public readonly struct FunctionEndpointGeneratorKnownTypes
{
    public FunctionEndpointGeneratorKnownTypes(
        INamedTypeSymbol functionAttribute,
        INamedTypeSymbol serviceBusTriggerAttribute,
        INamedTypeSymbol functionContext,
        INamedTypeSymbol cancellationToken,
        INamedTypeSymbol endpointConfiguration,
        INamedTypeSymbol iConfiguration,
        INamedTypeSymbol iHostEnvironment)
    {
        FunctionAttribute = functionAttribute;
        ServiceBusTriggerAttribute = serviceBusTriggerAttribute;
        FunctionContext = functionContext;
        CancellationToken = cancellationToken;
        EndpointConfiguration = endpointConfiguration;
        IConfiguration = iConfiguration;
        IHostEnvironment = iHostEnvironment;
    }

    public INamedTypeSymbol FunctionAttribute { get; }
    public INamedTypeSymbol ServiceBusTriggerAttribute { get; }
    public INamedTypeSymbol FunctionContext { get; }
    public INamedTypeSymbol CancellationToken { get; }
    public INamedTypeSymbol EndpointConfiguration { get; }
    public INamedTypeSymbol IConfiguration { get; }
    public INamedTypeSymbol IHostEnvironment { get; }

    public static bool TryGet(Compilation compilation, out FunctionEndpointGeneratorKnownTypes knownTypes)
    {
        var functionAttribute = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionAttribute");
        var serviceBusTriggerAttribute = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.ServiceBusTriggerAttribute");
        var functionContext = compilation.GetTypeByMetadataName("Microsoft.Azure.Functions.Worker.FunctionContext");
        var cancellationToken = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        var endpointConfiguration = compilation.GetTypeByMetadataName("NServiceBus.EndpointConfiguration");
        var iconfiguration = compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
        var iHostEnvironment = compilation.GetTypeByMetadataName("Microsoft.Extensions.Hosting.IHostEnvironment");

        if (functionAttribute is null
            || serviceBusTriggerAttribute is null
            || functionContext is null
            || cancellationToken is null
            || endpointConfiguration is null
            || iconfiguration is null
            || iHostEnvironment is null)
        {
            knownTypes = default;
            return false;
        }

        knownTypes = new FunctionEndpointGeneratorKnownTypes(
            functionAttribute,
            serviceBusTriggerAttribute,
            functionContext,
            cancellationToken,
            endpointConfiguration,
            iconfiguration,
            iHostEnvironment);

        return true;
    }
}
