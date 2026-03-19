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
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureSales(EndpointConfiguration configuration)
    {
        CommonEndpointConfig.Apply(configuration);

        configuration.RegisterComponents(services => services.AddSingleton(new MyComponent("Sales")));
        configuration.AddHandler<Handlers.SubmitOrderHandler>();
    }
}