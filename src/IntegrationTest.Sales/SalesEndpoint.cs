namespace IntegrationTest.Sales;

using Azure.Messaging.ServiceBus;
using IntegrationTest.Shared;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

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

    public static void ConfigureSales(EndpointConfiguration configuration, IServiceCollection services)
    {
        CommonEndpointConfig.Apply(configuration);

        services.AddSingleton(new MyComponent("Sales"));
        configuration.AddHandler<Handlers.SubmitOrderHandler>();
        configuration.AuditProcessedMessagesTo("audit");

        // Use the dead letter queue for failures
        configuration.Recoverability().CustomPolicy((_, context) =>
        {
            if (context.ImmediateProcessingFailures < 3)
            {
                return RecoverabilityAction.ImmediateRetry();
            }

            return RecoverabilityAction.DeadLetter(context.Exception);
        });
    }
}