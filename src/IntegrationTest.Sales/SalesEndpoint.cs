namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport.AzureServiceBus;

public partial class SalesEndpoint
{
    [NServiceBusFunction]
    [Function("Sales")]
    public partial Task Sales(
        [ServiceBusTrigger("sales", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = false)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        FunctionContext functionContext,
        CancellationToken cancellationToken = default);

    public static void ConfigureSales(EndpointConfiguration endpointConfiguration, IServiceCollection services)
    {
        CommonEndpointConfig.Apply(endpointConfiguration);

        services.AddSingleton(new MyComponent("Sales"));
        endpointConfiguration.AddHandler<Handlers.SubmitOrderHandler>();
        endpointConfiguration.AuditProcessedMessagesTo("audit");

        endpointConfiguration.Recoverability()
            .MoveErrorsToAzureServiceBusDeadLetterQueue();
    }
}