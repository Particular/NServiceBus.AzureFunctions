namespace IntegrationTest;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.AzureFunctions.AzureServiceBus;

public class ReceiverEndpointFunction([FromKeyedServices("ReceiverEndpoint")] IMessageProcessor processor)
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

public class AnotherReceiverEndpointFunction([FromKeyedServices("AnotherReceiverEndpoint")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}

public class AnotherReceiverEndpoint2Function([FromKeyedServices("AnotherReceiverEndpoint2")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint2")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint2", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}

//IDEA: Can we somehow use these as both the runtime hook and the manifest? Ie so that users can do; builder.AddNServiceBusFunction<AnotherReceiverEndpoint3Function>(c=>...)
public class AnotherReceiverEndpoint3Function([FromKeyedServices("AnotherReceiverEndpoint3")] IMessageProcessor processor)
{
    [Function("AnotherReceiverEndpoint3")]
    public Task Receiver(
        [ServiceBusTrigger("AnotherReceiverEndpoint3", Connection = "AzureWebJobsServiceBus", AutoCompleteMessages = true)]
        ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        return processor.Process(message, cancellationToken);
    }
}