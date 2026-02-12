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
        CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}

public class AnotherReceiverEndpoint([FromKeyedServices("AnotherReceiverEndpoint")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}

public class AnotherReceiverEndpoint2([FromKeyedServices("AnotherReceiverEndpoint2")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint2")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint2", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}