namespace NServiceBus.AzureFunctions.Analyzer;

static class KnownTypeNames
{
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
    public const string IServiceCollection = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    public const string IConfiguration = "Microsoft.Extensions.Configuration.IConfiguration";
    public const string IHostEnvironment = "Microsoft.Extensions.Hosting.IHostEnvironment";
    public const string AzureServiceBusFunctionsHostApplicationBuilderExtensions = "NServiceBus.Configuration.AdvancedExtensibility.AzureServiceBusFunctionsHostApplicationBuilderExtensions";
    public const string AddNServiceBusAzureServiceBusFunction = "AddNServiceBusAzureServiceBusFunction";
    public const string AddNServiceBusAzureServiceBusSendOnlyEndpoint = "AddNServiceBusAzureServiceBusSendOnlyEndpoint";
}