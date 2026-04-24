namespace NServiceBus.AzureFunctions.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using BitFaster.Caching;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Extensibility;
using Transport;
using NServiceBus.Transport.AzureServiceBus;
using Transport.AzureServiceBus.AdvancedExtensibility;
using static PipelineInvokingMessageProcessorLog;

class PipelineInvokingMessageProcessor(
    IMessageReceiver baseTransportReceiver,
    ICache<string, bool> messagesToBeCompleted,
    ILogger<PipelineInvokingMessageProcessor> logger,
    Func<ServiceBusReceivedMessage, Dictionary<string, string?>>? headerExtractor = null)
    : IMessageReceiver
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
        var nativeMessageId = message.GetMessageId();
        Dictionary<string, string?> headers;
        BinaryData body;
        var contextBag = new ContextBag();

        if (messagesToBeCompleted.TryRemove(nativeMessageId))
        {
            MessageAlreadyProcessed(logger, nativeMessageId);

            await SafeCompleteMessage(messageActions, nativeMessageId, message, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        try
        {
            headers = extractHeaders(message);
            body = message.GetBody();

            contextBag.Set(message);
        }
        catch (Exception ex)
        {
            MessageDeadLetteredDueToMetadataExtractionFailure(logger, ex);

            var deadLetterRequest = new DeadLetterRequest(ex);

            await SafeDeadLetterMessage(messageActions, message, deadLetterRequest, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        try
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();

            // we need to clone the headers since the core pipeline might mutate them
            var messageContext = new MessageContext(nativeMessageId, new Dictionary<string, string?>(headers), body, azureServiceBusTransportTransaction.TransportTransaction, ReceiveAddress, contextBag);

            await onMessage(messageContext, cancellationToken).ConfigureAwait(false);

            azureServiceBusTransportTransaction.Commit();

            await SafeCompleteMessage(messageActions, nativeMessageId, message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            MessageProcessingCanceled(logger, ex);
            await SafeAbandonMessage(messageActions, message, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            ErrorHandleResult errorHandleResult;
            try
            {
                // No need to clone the message header here since we do not make use of them after on error has executed
                var errorContext = new ErrorContext(exception, headers, nativeMessageId, body, azureServiceBusTransportTransaction.TransportTransaction, message.DeliveryCount, ReceiveAddress, contextBag);
                errorHandleResult = await onError.Invoke(errorContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                OnErrorCanceled(logger, ex);
                await SafeAbandonMessage(messageActions, message, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (ServiceBusException ex) when (ex.IsTransient || ex.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                OnErrorFailedDueToTransientException(logger, ex);
                await SafeAbandonMessage(messageActions, message, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                MessageDeadLetteredDueToExceptionInOnError(logger, exception);

                await SafeDeadLetterMessage(messageActions, message, new DeadLetterRequest(ex), CancellationToken.None).ConfigureAwait(false);
                return;
            }

            if (azureServiceBusTransportTransaction.TransportTransaction.TryGet<DeadLetterRequest>(out var applicationDeadLetterRequest))
            {
                UserRequestedDeadLetter(logger, nativeMessageId, applicationDeadLetterRequest.DeadLetterReason, applicationDeadLetterRequest.DeadLetterErrorDescription);

                await SafeDeadLetterMessage(messageActions, message, applicationDeadLetterRequest, CancellationToken.None).ConfigureAwait(false);

                return;
            }

            if (errorHandleResult == ErrorHandleResult.Handled)
            {
                azureServiceBusTransportTransaction.Commit();
                await SafeCompleteMessage(messageActions, nativeMessageId, message, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            await SafeAbandonMessage(messageActions, message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    async Task SafeDeadLetterMessage(ServiceBusMessageActions messageActions, ServiceBusReceivedMessage message, DeadLetterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            Dictionary<string, object>? propertiesToModify = null;

            if (request.PropertiesToModify != null)
            {
                propertiesToModify = new Dictionary<string, object>(request.PropertiesToModify);
            }

            await messageActions.DeadLetterMessageAsync(message,
                propertiesToModify,
                request.DeadLetterReason,
                request.DeadLetterErrorDescription,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            DeadLetterMessageFailed(logger, ex);
        }
    }

    async Task SafeAbandonMessage(ServiceBusMessageActions messageActions, ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await messageActions.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            AbandonMessageFailed(logger, ex);
        }
    }

    async Task SafeCompleteMessage(ServiceBusMessageActions messageActions, string nativeMessageId, ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await messageActions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            messagesToBeCompleted.AddOrUpdate(nativeMessageId, true);
        }
        catch (Exception ex)
        {
            CompleteMessageFailed(logger, ex);
            messagesToBeCompleted.AddOrUpdate(nativeMessageId, true);
        }
    }

    public Task StartReceive(CancellationToken cancellationToken = default) => Task.CompletedTask;

    // No-op because the rate at which Azure Functions pushes messages to the pipeline can't be controlled.
    public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopReceive(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ISubscriptionManager Subscriptions => baseTransportReceiver.Subscriptions;
    public string Id => baseTransportReceiver.Id;
    public string ReceiveAddress => baseTransportReceiver.ReceiveAddress;

    OnMessage onMessage = static (_, _) => Task.CompletedTask;
    OnError onError = static (_, _) => Task.FromResult(ErrorHandleResult.Handled);

    // we do this to enable tests to simulate exceptions when extracting headers
    readonly Func<ServiceBusReceivedMessage, Dictionary<string, string?>> extractHeaders = headerExtractor ?? (message => message.GetNServiceBusHeaders());
}

static partial class PipelineInvokingMessageProcessorLog
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Information,
        Message = "Message {MessageId} was already processed and will be completed")]
    internal static partial void MessageAlreadyProcessed(ILogger logger, string messageId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Message dead lettered due to issues with extracting message metadata.")]
    internal static partial void MessageDeadLetteredDueToMetadataExtractionFailure(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Message processing canceled.")]
    internal static partial void MessageProcessingCanceled(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "OnError canceled.")]
    internal static partial void OnErrorCanceled(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "OnError failed due to transient exception.")]
    internal static partial void OnErrorFailedDueToTransientException(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message = "Message dead lettered due to exception in OnError.")]
    internal static partial void MessageDeadLetteredDueToExceptionInOnError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "User requested {MessageId} to be dead lettered due to {DeadLetterReason}: {DeadLetterErrorDescription}")]
    internal static partial void UserRequestedDeadLetter(ILogger logger, string messageId, string deadLetterReason, string deadLetterErrorDescription);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "Dead letter message failed.")]
    internal static partial void DeadLetterMessageFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Warning,
        Message = "Abandon message failed.")]
    internal static partial void AbandonMessageFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "Complete message failed.")]
    internal static partial void CompleteMessageFailed(ILogger logger, Exception exception);
}