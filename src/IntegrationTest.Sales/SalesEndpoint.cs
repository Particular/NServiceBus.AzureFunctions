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

    public static void ConfigureSales(EndpointConfiguration configuration, IServiceCollection services)
    {
        CommonEndpointConfig.Apply(configuration);

        services.AddSingleton(new MyComponent("Sales"));
        configuration.AddHandler<Handlers.SubmitOrderHandler>();
        configuration.AuditProcessedMessagesTo("audit");

        configuration.Recoverability()
            .MoveErrorsToAzureServiceBusDeadLetterQueue();
    }
}