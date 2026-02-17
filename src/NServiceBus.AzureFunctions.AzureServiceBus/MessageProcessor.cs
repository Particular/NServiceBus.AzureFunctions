namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using NServiceBus.MultiHosting;

public class MessageProcessor(AzureServiceBusServerlessTransport transport, EndpointStarter endpointStarter)
{
    public async Task Process(ServiceBusReceivedMessage message, FunctionContext functionContext, CancellationToken cancellationToken = default)
    {
        var endpointName = endpointStarter.ServiceKey;

        var logger = functionContext.GetLogger("MessageProcessor");

        //TODO: Should we add things like the MessageId?
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["Endpoint"] = endpointName });

        using var _ = MultiEndpointLoggerFactory.Instance.PushName(endpointStarter.ServiceKey);

        await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);

        if (transport.MessageProcessor is null)
        {
            // This should never happen but we need to protect against it anyways
            throw new InvalidOperationException($"Endpoint {endpointName} cannot process messages because it is configured in send-only mode.");
        }

        await transport.MessageProcessor.Process(message, cancellationToken).ConfigureAwait(false);
    }
}