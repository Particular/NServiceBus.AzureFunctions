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
        string nativeMessageId;
        Dictionary<string, string?> headers;
        BinaryData body;
        var contextBag = new ContextBag();

        try
        {
            nativeMessageId = GetNativeMessageId(message);
            headers = extractHeaders(message);
            body = message.Body ?? BinaryData.FromBytes(ReadOnlyMemory<byte>.Empty);

            contextBag.Set(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Message dead lettered due to issues with extracting message metadata.");

            await DeadLetterMessage(messageActions, message, new DeadLetterRequest(ex), CancellationToken.None).ConfigureAwait(false);
            return;
        }

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
                // No need to clone the message header here since we do not make use of them after on error has executed
                var errorContext = new ErrorContext(exception, headers, nativeMessageId, body, azureServiceBusTransportTransaction.TransportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);
                errorHandleResult = await onError!.Invoke(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogDebug(ex, "OnError canceled.");
                await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (ServiceBusException ex) when (ex.IsTransient || ex.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                logger.LogWarning(ex, "OnError failed due to transient exception.");
                await messageActions.AbandonMessageAsync(message, cancellationToken: CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(exception, "Message dead lettered due to exception in OnError.");

                await DeadLetterMessage(messageActions, message, new DeadLetterRequest(ex), CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (azureServiceBusTransportTransaction.TransportTransaction.TryGet<DeadLetterRequest>(out var deadLetterRequest))
            {
                logger.LogError($"User requested {nativeMessageId} to be dead lettered due to {deadLetterRequest.DeadLetterReason}: {deadLetterRequest.DeadLetterErrorDescription}");

                await DeadLetterMessage(messageActions, message, deadLetterRequest, CancellationToken.None).ConfigureAwait(false);

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

    Task DeadLetterMessage(ServiceBusMessageActions messageActions, ServiceBusReceivedMessage message, DeadLetterRequest request, CancellationToken cancellationToken) =>
        messageActions.DeadLetterMessageAsync(message,
            request.PropertiesToModify,
            request.DeadLetterReason,
            request.DeadLetterErrorDescription,
            cancellationToken);

    static string GetNativeMessageId(ServiceBusReceivedMessage message)
    {
        if (string.IsNullOrEmpty(message.MessageId))
        {
            throw new Exception("Azure Service Bus MessageId is required, but was not found. Ensure to assign MessageId to all Service Bus messages.");
        }

        return message.MessageId;
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