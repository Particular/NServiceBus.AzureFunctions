namespace NServiceBus.AzureFunctions.AzureServiceBus.Serverless.TransportWrapper;

using System;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using NServiceBus.Extensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.AzureServiceBus;

class PipelineInvokingMessageProcessor(IMessageReceiver baseTransportReceiver) : IMessageReceiver
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
        var messageId = message.MessageId ?? Guid.NewGuid().ToString("N");
        var body = GetBody(message);
        var contextBag = new ContextBag();
        // Azure Service Bus transport also makes the incoming message available. We can do the same narrow the gap
        contextBag.Set(message);

        try
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            var messageContext = CreateMessageContext(message, messageId, body, azureServiceBusTransportTransaction.TransportTransaction, contextBag);

            await onMessage!(messageContext, cancellationToken).ConfigureAwait(false);

            azureServiceBusTransportTransaction.Commit();
            await messageActions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await messageActions.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            using var azureServiceBusTransportTransaction = new AzureServiceBusTransportTransaction();
            var errorContext = CreateErrorContext(message, exception, messageId, body, azureServiceBusTransportTransaction.TransportTransaction, contextBag);

            var errorHandleResult = await onError!.Invoke(errorContext, cancellationToken).ConfigureAwait(false);

            if (errorHandleResult == ErrorHandleResult.Handled)
            {
                azureServiceBusTransportTransaction.Commit();
                return;
            }

            await messageActions.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    static BinaryData GetBody(ServiceBusReceivedMessage message)
    {
        var body = message.Body ?? BinaryData.FromBytes(ReadOnlyMemory<byte>.Empty);
        var memory = body.ToMemory();

        if (memory.IsEmpty ||
            !message.ApplicationProperties.TryGetValue(TransportEncodingHeader, out var value) ||
            !value.Equals("wcf/byte-array"))
        {
            return body;
        }

        using var reader = XmlDictionaryReader.CreateBinaryReader(body.ToStream(), XmlDictionaryReaderQuotas.Max);
        var bodyBytes = (byte[])Deserializer.ReadObject(reader)!;
        return new BinaryData(bodyBytes);
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

        headers.Remove(TransportEncodingHeader);

        if (!string.IsNullOrWhiteSpace(message.ReplyTo))
        {
            headers[Headers.ReplyToAddress] = message.ReplyTo;
        }

        if (!string.IsNullOrWhiteSpace(message.CorrelationId))
        {
            headers[Headers.CorrelationId] = message.CorrelationId;
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

    const string TransportEncodingHeader = "NServiceBus.Transport.Encoding";

    static readonly DataContractSerializer Deserializer = new(typeof(byte[]));
}