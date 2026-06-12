namespace NServiceBus.AzureFunctions.Analyzer;

static class KnownTypeNames
{
    public static string ConfigureMethodName(string endpointName)
    {
        if (endpointName == null)
        {
            throw new ArgumentNullException(nameof(endpointName));
        }

        // Unfortunately, we cannot use SearchValues and ReplaceAny because we are stuck with NetStandard2.0 and those APIs are only available in .NET.
        // Pooling is not helpful here because it would make things slower
        var buffer = new char[endpointName.Length];
        var count = 0;

        foreach (var c in endpointName)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                buffer[count++] = c;
            }
        }

        if (count == 0)
        {
            throw new ArgumentException(
                $"Cannot generate a valid C# configuration method name from endpoint name '{endpointName}' because it does not contain any ASCII letters, digits, or underscores.",
                nameof(endpointName));
        }

        return count == endpointName.Length ? $"Configure{endpointName}" : $"Configure{new string(buffer, 0, count)}";
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