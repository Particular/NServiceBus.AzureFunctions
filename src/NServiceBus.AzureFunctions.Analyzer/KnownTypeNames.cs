namespace NServiceBus.AzureFunctions.Analyzer;

static class KnownTypeNames
{
    public static string ConfigureMethodName(string endpointName)
    {
        var sanitized = new string([.. endpointName.Where(char.IsLetterOrDigit)]);

        return sanitized.Length == 0 ? throw new ArgumentException($"Invalid {endpointName}") : $"Configure{sanitized}";
    }
    public const string GeneratedCompositionNamespace = "NServiceBus";
    public const string GeneratedFunctionsCompositionFullName = "NServiceBus.NServiceBusGeneratedFunctionsComposition";
    public const string FunctionAttribute = "Microsoft.Azure.Functions.Worker.FunctionAttribute";
    public const string FunctionContext = "Microsoft.Azure.Functions.Worker.FunctionContext";
    public const string NServiceBusFunctionAttribute = "NServiceBus.NServiceBusFunctionAttribute";
    public const string NServiceBusSendOnlyFunctionAttribute = "NServiceBus.NServiceBusSendOnlyFunctionAttribute";
    public const string CancellationToken = "System.Threading.CancellationToken";
    public const string EndpointConfigurationType = "NServiceBus.EndpointConfiguration";
    public const string IHandleMessages = "NServiceBus.IHandleMessages`1";
    public const string SendOptions = "NServiceBus.SendOptions";
    public const string ReplyOptions = "NServiceBus.ReplyOptions";
    public const string AzureServiceBusServerlessTransport = "NServiceBus.AzureServiceBusServerlessTransport";
    public const string FunctionEndpointConfiguration = "NServiceBus.FunctionEndpointConfiguration";
    public const string AzureServiceBusFunctionsHostApplicationBuilderExtensions = "NServiceBus.Configuration.AdvancedExtensibility.AzureServiceBusFunctionsHostApplicationBuilderExtensions";
    public const string AddNServiceBusAzureServiceBusFunction = "AddNServiceBusAzureServiceBusFunction";
    public const string AddNServiceBusAzureServiceBusSendOnlyEndpoint = "AddNServiceBusAzureServiceBusSendOnlyEndpoint";
    public const string FunctionsApplicationBuilder = "Microsoft.Azure.Functions.Worker.Builder.FunctionsApplicationBuilder";
    public const string GeneratedFunctionsCompositionClassName = "NServiceBusGeneratedFunctionsComposition";
    public const string GeneratedFunctionsCompositionRegisterMethodName = "Register";
}