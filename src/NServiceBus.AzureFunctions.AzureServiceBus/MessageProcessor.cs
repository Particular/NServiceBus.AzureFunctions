namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using NServiceBus.MultiHosting;

public class MessageProcessor(AzureServiceBusServerlessTransport transport, EndpointStarter endpointStarter) : IMessageProcessor
{
    public async Task Process(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions,
        FunctionContext functionContext, CancellationToken cancellationToken = default)
    {
        using var _ = MultiEndpointLoggerFactory.Instance.PushName(endpointStarter.ServiceKey);
        await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await transport.MessageProcessor.Process(message, messageActions, cancellationToken).ConfigureAwait(false);
    }
}