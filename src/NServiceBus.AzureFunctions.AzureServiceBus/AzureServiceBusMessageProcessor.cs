namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

public class AzureServiceBusMessageProcessor(AzureServiceBusServerlessTransport transport, string endpointName)
{
    public async Task Process(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, FunctionContext functionContext, CancellationToken cancellationToken = default)
    {
        if (transport.MessageProcessor is null)
        {
            // This should never happen but we need to protect against it anyways
            throw new InvalidOperationException($"Endpoint {endpointName} cannot process messages because it is configured in send-only mode.");
        }

        await transport.MessageProcessor.Process(message, messageActions, cancellationToken).ConfigureAwait(false);
    }
}