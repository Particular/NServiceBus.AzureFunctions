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

class PipelineInvokingMessageProcessor : IMessageReceiver
{
    public PipelineInvokingMessageProcessor(IMessageReceiver baseTransportReceiver,
        ILogger<PipelineInvokingMessageProcessor> logger,
        Func<ServiceBusReceivedMessage, Dictionary<string, string?>>? headerExtractor = null)
    {
        this.baseTransportReceiver = baseTransportReceiver;
        this.logger = logger;

        extractHeaders = headerExtractor ?? GetNServiceBusHeaders;
    }

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

        Dictionary<string, string?> headers;
        try
        {
            headers = extractHeaders(message);
        }
        catch (Exception ex)
        {
            const string deadLetterReason = "Failed to extract headers from message.";

            await messageActions.DeadLetterMessageAsync(message,
                deadLetterReason: deadLetterReason,
                deadLetterErrorDescription: ex.ToString(), cancellationToken: CancellationToken.None).ConfigureAwait(false);

            logger.LogError(deadLetterReason);
            return;
        }

        var body = message.Body ?? BinaryData.FromBytes(ReadOnlyMemory<byte>.Empty);

        var contextBag = new ContextBag();

        // Azure Service Bus transport also makes the incoming message available. We can do the same narrow the gap
        contextBag.Set(message);

        try
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();

            // we need to clone the headers since the core pipeline might mutate them
            var messageContext = new MessageContext(nativeMessageId, new Dictionary<string, string?>(headers), body, azureServiceBusTransportTransaction.TransportTransaction, ReceiveAddress, contextBag);

            await onMessage!(messageContext, cancellationToken).ConfigureAwait(false);

            azureServiceBusTransportTransaction.Commit();
            await messageActions.CompleteMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(ex, "Message processing canceled.");
            await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            ErrorHandleResult errorHandleResult;
            try
            {
                var errorContext = new ErrorContext(exception, headers, nativeMessageId, body, azureServiceBusTransportTransaction.TransportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);
                errorHandleResult = await onError!.Invoke(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "OnError canceled.");
                await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                //TODO: The transport has a circuit breaker for repeated failures, should we go with something similar? we could do a LRU cache and then dead letter after X retries
                await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                logger.LogWarning(ex, "Failed to execute onError");
                return;
            }

            if (azureServiceBusTransportTransaction.TransportTransaction.TryGet<DeadLetterRequest>(out var deadLetterRequest))
            {
                await messageActions.DeadLetterMessageAsync(message, deadLetterRequest.PropertiesToModify, deadLetterRequest.DeadLetterReason, deadLetterRequest.DeadLetterErrorDescription, cancellationToken: CancellationToken.None).ConfigureAwait(false);

                logger.LogError($"User requested {nativeMessageId} to be dead lettered due to {deadLetterRequest.DeadLetterReason}: {deadLetterRequest.DeadLetterErrorDescription}");
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

    Dictionary<string, string?> GetNServiceBusHeaders(ServiceBusReceivedMessage message)
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

    readonly IMessageReceiver baseTransportReceiver;
    readonly ILogger<PipelineInvokingMessageProcessor> logger;
    readonly Func<ServiceBusReceivedMessage, Dictionary<string, string?>> extractHeaders;
}