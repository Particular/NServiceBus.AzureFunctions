namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;

/// <summary>
/// Azure Service Bus Message Processor that gets used by the source generator to process messages received from Azure Service Bus and forward them to the NServiceBus pipeline.
/// This type should only be used by the source generator.
/// </summary>
/// <remarks>The API surface might be changed between versions according to the needs of the source generator.</remarks>
/// <param name="transport">The AzureServiceBusServerlessTransport to be used.</param>
/// <param name="endpointName">The endpoint name.</param>
public class AzureServiceBusMessageProcessor(AzureServiceBusServerlessTransport transport, string endpointName)
{
    /// <summary>
    /// Processes the received <see cref="ServiceBusReceivedMessage"/>
    /// </summary>
    public async Task Process(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, FunctionContext functionContext, CancellationToken cancellationToken = default)
    {
        if (transport.MessageProcessor is null)
        {
            // This should never happen but we need to protect against it anyway
            throw new InvalidOperationException($"Endpoint {endpointName} cannot process messages because it is configured in send-only mode.");
        }

        await transport.MessageProcessor.Process(message, messageActions, cancellationToken).ConfigureAwait(false);
    }
}