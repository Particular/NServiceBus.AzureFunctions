namespace IntegrationTest;

using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

public class AnotherReceiverEndpoint([FromKeyedServices("AnotherReceiverEndpoint")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint")]
    public Task Receiver(
        //TODO: If we source gen via the trigger we might be able to pickup the connection setting and use it
        [ServiceBusTrigger("AnotherReceiverEndpoint", Connection = "fzvsdfg", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions, FunctionContext context, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, messageActions, context, cancellationToken);
    }
}