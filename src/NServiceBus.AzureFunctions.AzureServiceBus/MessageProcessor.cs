namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;


public class MessageProcessor(ServerlessTransport transport, EndpointStarter endpointStarter) : IMessageProcessor
{
    public async Task Process(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions,
        FunctionContext functionContext, CancellationToken cancellationToken = default)
    {
        using var _ = FunctionsLoggerFactory.Instance.PushName(endpointStarter.ServiceKey);
        await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await transport.MessageProcessor.Process(message, messageActions, cancellationToken).ConfigureAwait(false);
    }
}