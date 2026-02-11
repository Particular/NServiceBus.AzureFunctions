namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
public interface IMessageProcessor
{
    Task Process(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default);
}