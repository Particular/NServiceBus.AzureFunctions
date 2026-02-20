namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;

// Cleanest pattern for single-function endpoints
[NServiceBusFunction]
public partial class SalesEndpoint
{
    [Function("Sales")]
    public partial Task Sales(
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureSales(EndpointConfiguration configuration, IServiceCollection serviceCollection)
    {
        CommonEndpointConfig.Apply(configuration);

        serviceCollection.AddSingleton(new MyComponent("Sales"));
        configuration.AddHandler<Handlers.AcceptOrderHandler>();
    }
}

public class MyComponent(string endpointName)
{
    public string EndpointName => endpointName;
}