namespace NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

interface IMessageProcessor
{
    Task Process(ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken = default);
}