namespace IntegrationTest;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

public class ReceiverEndpoint([FromKeyedServices("ReceiverEndpoint")] IMessageProcessor processor)
{
    [Function("ReceiverEndpoint")]
    public Task Receiver(
        [ServiceBusTrigger("ReceiverEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions, FunctionContext context, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, messageActions, context, cancellationToken);
    }
}

public class AnotherReceiverEndpoint([FromKeyedServices("AnotherReceiverEndpoint")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions, FunctionContext context, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, messageActions, context, cancellationToken);
    }
}