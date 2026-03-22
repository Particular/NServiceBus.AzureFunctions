namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

class PipelineInvokingMessageProcessor(IMessageReceiver baseTransportReceiver, ILogger<PipelineInvokingMessageProcessor> logger) : IMessageReceiver
{
    public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError,
        CancellationToken cancellationToken = default)
    {
        this.onMessage = onMessage;
        this.onError = onError;
        return baseTransportReceiver.Initialize(limitations,
            (_, __) => Task.CompletedTask,
            (_, __) => Task.FromResult(ErrorHandleResult.Handled),
            cancellationToken) ?? Task.CompletedTask;
    }

    public async Task Process(ServiceBusReceivedMessage message, ServiceBusMessageActions messageActions, CancellationToken cancellationToken = default)
    {
        var nativeMessageId = message.MessageId;
        if (string.IsNullOrEmpty(nativeMessageId))
        {
            const string deadLetterErrorDescription = "Azure Service Bus MessageId is required, but was not found. Ensure to assign MessageId to all Service Bus messages.";

            await messageActions.DeadLetterMessageAsync(message, deadLetterReason: "MessageId not set on message", deadLetterErrorDescription: deadLetterErrorDescription, cancellationToken: CancellationToken.None).ConfigureAwait(false);

            logger.LogError(deadLetterErrorDescription);
            return;
        }

        var body = message.Body ?? BinaryData.FromBytes(ReadOnlyMemory<byte>.Empty);

        //TODO: Should we get the headers up here as well

        var contextBag = new ContextBag();

        // Azure Service Bus transport also makes the incoming message available. We can do the same narrow the gap
        contextBag.Set(message);

        try
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            var messageContext = CreateMessageContext(message, nativeMessageId, body, azureServiceBusTransportTransaction.TransportTransaction, contextBag);

            await onMessage!(messageContext, cancellationToken).ConfigureAwait(false);

            azureServiceBusTransportTransaction.Commit();
            await messageActions.CompleteMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            var errorContext = CreateErrorContext(message, exception, nativeMessageId, body, azureServiceBusTransportTransaction.TransportTransaction, contextBag);

            var errorHandleResult = await onError!.Invoke(errorContext, CancellationToken.None).ConfigureAwait(false);

            if (errorContext.TransportTransaction.TryGet<DeadLetterRequest>(out var deadLetterRequest))
            {
                await messageActions.DeadLetterMessageAsync(message, deadLetterRequest.PropertiesToModify, deadLetterRequest.DeadLetterReason, deadLetterRequest.DeadLetterErrorDescription, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (errorHandleResult == ErrorHandleResult.Handled)
            {
                azureServiceBusTransportTransaction.Commit();
                await messageActions.CompleteMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }

    ErrorContext CreateErrorContext(ServiceBusReceivedMessage message, Exception exception, string messageId,
        BinaryData body, TransportTransaction transportTransaction, ContextBag contextBag) =>
        new(exception, GetNServiceBusHeaders(message), messageId, body, transportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);

    MessageContext CreateMessageContext(ServiceBusReceivedMessage message, string messageId, BinaryData body,
        TransportTransaction transportTransaction, ContextBag contextBag) =>
        new(messageId, GetNServiceBusHeaders(message), body, transportTransaction, ReceiveAddress, contextBag);

    static Dictionary<string, string?> GetNServiceBusHeaders(ServiceBusReceivedMessage message)
    {
        var headers = new Dictionary<string, string?>(message.ApplicationProperties.Count);

        foreach (var kvp in message.ApplicationProperties)
        {
            headers[kvp.Key] = kvp.Value?.ToString();
        }

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            headers[Headers.ReplyToAddress] = message.ReplyTo;
        }

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            headers[Headers.CorrelationId] = message.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(message.ContentType))
        {
            headers[Headers.ContentType] = message.ContentType;
        }

        return headers;
    }

    public Task StartReceive(CancellationToken cancellationToken = default) => Task.CompletedTask;

    // No-op because the rate at which Azure Functions pushes messages to the pipeline can't be controlled.
    public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopReceive(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ISubscriptionManager Subscriptions => baseTransportReceiver.Subscriptions;
    public string Id => baseTransportReceiver.Id;
    public string ReceiveAddress => baseTransportReceiver.ReceiveAddress;

    OnMessage? onMessage;
    OnError? onError;
}