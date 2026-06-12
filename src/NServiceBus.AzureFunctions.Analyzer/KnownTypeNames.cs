namespace NServiceBus.AzureFunctions.Analyzer;

static class KnownTypeNames
{
    const string ConfigurePrefix = "Configure";

    public static string ConfigureMethodName(string endpointName)
    {
        if (endpointName == null)
        {
            throw new ArgumentNullException(nameof(endpointName));
        }

        var normalized = Normalize(endpointName);
        if (normalized.Length == 0)
        {
            throw new ArgumentException(
                $"Cannot generate a valid C# configuration method name from endpoint name '{endpointName}' because it does not contain any ASCII letters or digits.",
                nameof(endpointName));
        }

        return $"{ConfigurePrefix}{normalized}";
    }

    public static string Normalize(string name)
    {
        if (name == null)
        {
            return string.Empty;
        }

        var buffer = new char[name.Length];
        var count = 0;

        foreach (var c in name)
        {
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                buffer[count++] = c;
            }
        }

        return count == 0 ? string.Empty : count == name.Length ? name : new string(buffer, 0, count);
    }

    public static bool IsConfigureMethodFor(string methodName, string normalizedEndpointName)
    {
        if (!methodName.StartsWith(ConfigurePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (methodName.Length == ConfigurePrefix.Length)
        {
            return false;
        }

        var suffix = methodName.Substring(ConfigurePrefix.Length);
        var normalizedSuffix = Normalize(suffix);

        return normalizedSuffix.Length > 0
            && string.Equals(normalizedSuffix, normalizedEndpointName, StringComparison.OrdinalIgnoreCase);
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