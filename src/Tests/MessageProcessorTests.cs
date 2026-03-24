namespace NServiceBus.AzureFunctions.Tests;

using Azure.Messaging.ServiceBus;
using AzureServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Testing;
using NUnit.Framework;
using Transport;
using NServiceBus;

[TestFixture]
public class MessageProcessorTests
{
    [Test]
    public async Task Should_expose_native_message_id_headers_and_body_on_message_context()
    {
        var expectedMessageId = "test-message-id-123";
        var expectedBody = new byte[] { 1, 2, 3, 4 };
        var expectedHeaderKey = "custom-header";
        var expectedHeaderValue = "header-value";
        var expectedReplyTo = "reply-queue";
        var expectedCorrelationId = "correlation-abc";
        var expectedContentType = "some/content-type";

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: expectedMessageId,
            properties: new Dictionary<string, object> { { expectedHeaderKey, expectedHeaderValue } },
            body: new BinaryData(expectedBody),
            replyTo: expectedReplyTo,
            correlationId: expectedCorrelationId,
            contentType: expectedContentType
        );

        var result = await ProcessMessage(
            message: message
        );

        var messageContext = result.MessageContext;

        using (Assert.EnterMultipleScope())
        {
            Assert.NotNull(messageContext, "MessageContext should not be null");
            Assert.AreEqual(expectedMessageId, messageContext!.NativeMessageId, "MessageContext should expose the native message id");
            Assert.IsTrue(messageContext.Headers.ContainsKey(expectedHeaderKey), "MessageContext should expose the custom header");
            Assert.AreEqual(expectedHeaderValue, messageContext.Headers[expectedHeaderKey], "MessageContext should expose the correct header value");
            Assert.That(messageContext.Body.ToArray(), Is.EqualTo(expectedBody).AsCollection, "MessageContext should expose the correct message body");
            Assert.IsTrue(messageContext.Headers.ContainsKey(Headers.CorrelationId), "Native CorrelationId should be upconverted to the CorrelationId header");
            Assert.AreEqual(expectedCorrelationId, messageContext.Headers[Headers.CorrelationId], "Headers should expose the correct CorrelationId value");
            Assert.IsTrue(messageContext.Headers.ContainsKey(Headers.ReplyToAddress), "Native ReplyTo should be upconverted to the ReplyToAddress header");
            Assert.AreEqual(expectedReplyTo, messageContext.Headers[Headers.ReplyToAddress], "Headers should expose the correct ReplyTo header value");
            Assert.IsTrue(messageContext.Headers.ContainsKey(Headers.ContentType), "Native ContentType should be upconverted to the ContentType header");
            Assert.AreEqual(expectedContentType, messageContext.Headers[Headers.ContentType], "Headers should expose the correct ContentType header value");
        }
    }

    [Test]
    public async Task Should_require_native_message_id_to_be_set()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();
        var result = await ProcessMessage(message: message);
        using (Assert.EnterMultipleScope())
        {
            Assert.False(result.OnMessageWasCalled, "OnMessage should not be called");
            Assert.False(result.OnErrorWasCalled, "OnError should not be called");
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
        }
    }

    [Test]
    public async Task Should_complete_when_on_message_succeeds()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => Task.CompletedTask);

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled, "OnMessage should be called");
            Assert.IsFalse(result.OnErrorWasCalled, "OnError should not be called");
            Assert.IsTrue(result.MessageActions.WasCompleted, "Message should be completed");
            Assert.IsFalse(result.MessageActions.WasAbandoned, "Message should not be abandoned");
        }
    }

    [Test]
    public async Task Should_abandon_when_on_message_fails_and_retry_is_requested()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"));

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled, "OnMessage should be called");
            Assert.IsTrue(result.OnErrorWasCalled, "OnError should be called");
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsTrue(result.MessageActions.WasAbandoned, "Message should be abandoned");
        }
    }

    [Test]
    public async Task Should_complete_when_on_message_fails_and_failure_is_marked_as_handled()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (_, _) => Task.FromResult(ErrorHandleResult.Handled));

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled, "OnMessage should be called");
            Assert.IsTrue(result.OnErrorWasCalled, "OnError should be called");
            Assert.IsTrue(result.MessageActions.WasCompleted, "Message should be completed");
            Assert.IsFalse(result.MessageActions.WasAbandoned, "Message should not be abandoned");
        }
    }

    [Test]
    public async Task Should_abandon_when_on_error_throws_transient_service_bus_exception()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (_, _) => throw new ServiceBusException("simulated transient exception", ServiceBusFailureReason.ServiceBusy)); //ServiceBusy is transient

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsTrue(result.MessageActions.WasAbandoned, "Message should be abandoned if onError throws");
            Assert.IsFalse(result.MessageActions.WasDeadLettered, "Message should not be dead lettered");
        }
    }

    [Test]
    public async Task Should_abandon_when_on_error_throws_lock_lost_service_bus_exception()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (_, _) => throw new ServiceBusException("simulated lock lost exception", ServiceBusFailureReason.MessageLockLost));

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsTrue(result.MessageActions.WasAbandoned, "Message should be abandoned if onError throws");
            Assert.IsFalse(result.MessageActions.WasDeadLettered, "Message should not be dead lettered");
        }
    }

    [Test]
    public async Task Should_dlq_when_on_error_throws_non_transient_exception()
    {
        var exception = new Exception("simulated exception in on error");
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (_, _) => throw exception);

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsFalse(result.MessageActions.WasAbandoned, "Message should not be abandoned if onError throws");
            AssertExceptionWasDeadLettered(result, exception);
        }
    }

    [Test]
    public async Task Should_dlq_message_if_requested()
    {
        var expectedDlqReason = "some reason";
        var expectedDlqDescription = "some description";

        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (errorContext, _) =>
            {
                errorContext.TransportTransaction.Set(new DeadLetterRequest(expectedDlqReason, expectedDlqDescription, new Dictionary<string, object> { { "MyProperty", "MyValue" } }));
                return Task.FromResult(ErrorHandleResult.Handled);
            });

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsFalse(result.MessageActions.WasAbandoned, "Message should not be abandoned");
            Assert.IsTrue(result.MessageActions.WasDeadLettered, "Message should be dead lettered");
            Assert.AreEqual(result.MessageActions.DeadLetterDetails?.DeadLetterReason, expectedDlqReason);
            Assert.AreEqual(result.MessageActions.DeadLetterDetails?.DeadLetterErrorDescription, expectedDlqDescription);
            Assert.AreEqual(result.MessageActions.DeadLetterDetails?.DeadLetterProperties?["MyProperty"], "MyValue");
            Assert.AreEqual(result.LogCollector.LatestRecord.Level, Microsoft.Extensions.Logging.LogLevel.Error, "DLQ requests should be logged as error");
            Assert.True(result.LogCollector.LatestRecord.Message.Contains(expectedDlqReason), "Should log DLQ reason");
            Assert.True(result.LogCollector.LatestRecord.Message.Contains(expectedDlqDescription), "Should log DLQ description");
        }
    }

    [Test]
    public async Task Should_expose_the_service_bus_message_on_both_message_and_error_context()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: Guid.NewGuid().ToString());

        var result = await ProcessMessage(
            message: message,
            onMessage: (_, _) => throw new Exception("simulated exception"));

        using (Assert.EnterMultipleScope())
        {
            Assert.AreSame(message, result.MessageContext?.Extensions.Get<ServiceBusReceivedMessage>(), "MessageContext should contain the ServiceBusReceivedMessage");
            Assert.AreSame(message, result.ErrorContext?.Extensions.Get<ServiceBusReceivedMessage>(), "ErrorContext should contain the ServiceBusReceivedMessage");
        }
    }

    [Test]
    public async Task Should_abandon_when_token_is_cancelled_and_not_invoke_onerror()
    {
        var result = await ProcessMessage(
            onMessage: (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            cancellationToken: new CancellationToken(true)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.OnErrorWasCalled, "OnError should not be called");
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsTrue(result.MessageActions.WasAbandoned, "Message should be abandoned");
            Assert.AreEqual(result.LogCollector.LatestRecord.Level, Microsoft.Extensions.Logging.LogLevel.Debug, "Cancellation should be logged as debug");
            Assert.True(result.LogCollector.LatestRecord.Message.Contains("Message processing canceled"), "Should log debug when processing canceled");
            Assert.IsInstanceOf<OperationCanceledException>(result.LogCollector.LatestRecord.Exception);
        }
    }

    [Test]
    public async Task Should_abandon_on_error_when_token_is_cancelled()
    {
        var result = await ProcessMessage(
            onMessage: (_, _) => throw new Exception("simulated exception"),
            onError: (_, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(ErrorHandleResult.Handled);
            },
            cancellationToken: new CancellationToken(true)
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.IsFalse(result.MessageActions.WasCompleted, "Message should not be completed");
            Assert.IsTrue(result.MessageActions.WasAbandoned, "Message should be abandoned");
            Assert.AreEqual(result.LogCollector.LatestRecord.Level, Microsoft.Extensions.Logging.LogLevel.Debug, "Cancellation should be logged as debug");
            Assert.True(result.LogCollector.LatestRecord.Message.Contains("OnError canceled"), "Should log debug when on error canceled");
            Assert.IsInstanceOf<OperationCanceledException>(result.LogCollector.LatestRecord.Exception);
        }
    }

    [Test]
    public async Task Should_not_propagate_header_mutations_from_on_message_to_on_error()
    {
        var originalHeaderKey = "original-header";
        var originalHeaderValue = "original-value";
        var addedHeaderKey = "added-header";

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: Guid.NewGuid().ToString(),
            properties: new Dictionary<string, object> { { originalHeaderKey, originalHeaderValue } }
        );

        var result = await ProcessMessage(
            message: message,
            onMessage: (msgContext, _) =>
            {
                msgContext.Headers[addedHeaderKey] = "some-value";
                msgContext.Headers[originalHeaderKey] = "some-other-value";
                throw new Exception("force error");
            }
        );

        var headers = result.ErrorContext!.Message.Headers;
        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(headers.ContainsKey(originalHeaderKey), "Original header should still exist in onError");
            Assert.AreEqual(originalHeaderValue, headers[originalHeaderKey], "Original header value should be preserved in onError");
            Assert.IsFalse(headers.ContainsKey(addedHeaderKey), "Added header should NOT be present in onError");
        }
    }

    [Test]
    public async Task Should_dead_letter_if_header_extraction_fails()
    {
        var exception = new Exception("simulated exception");
        var result = await ProcessMessage(headerExtractor: _ => throw exception);

        using (Assert.EnterMultipleScope())
        {
            Assert.False(result.OnMessageWasCalled, "OnMessage should not be called if header extraction fails");
            Assert.False(result.OnErrorWasCalled, "OnError should not be called if header extraction fails");
            Assert.True(result.MessageActions.WasDeadLettered, "Message should be dead lettered");
        }
    }

    void AssertExceptionWasDeadLettered(ProcessingResult result, Exception exception)
    {
        Assert.True(result.MessageActions.WasDeadLettered, "Message should be dead lettered");

        // Make sure we follow microsoft guidance - https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues#application-level-dead-lettering
        Assert.AreEqual(result.MessageActions.DeadLetterDetails?.DeadLetterReason, $"{exception.GetType().FullName!} - {exception.Message}");
        Assert.AreEqual(result.MessageActions.DeadLetterDetails?.DeadLetterErrorDescription, exception.StackTrace);
        Assert.AreEqual(Microsoft.Extensions.Logging.LogLevel.Error, result.LogCollector.LatestRecord.Level, "Dead lettering be logged as error");
        Assert.AreEqual(result.LogCollector.LatestRecord.Exception, exception);
    }

    async Task<ProcessingResult> ProcessMessage(
        ServiceBusReceivedMessage? message = null,
        Func<MessageContext, CancellationToken, Task>? onMessage = null,
        Func<ErrorContext, CancellationToken, Task<ErrorHandleResult>>? onError = null,
        Func<ServiceBusReceivedMessage, Dictionary<string, string?>>? headerExtractor = null,
        CancellationToken cancellationToken = default)
    {
        message ??= ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: Guid.NewGuid().ToString());
        onMessage ??= (_, _) => Task.CompletedTask;
        onError ??= (_, _) => Task.FromResult(ErrorHandleResult.RetryRequired);

        var fakeLogger = new FakeLogger<PipelineInvokingMessageProcessor>();
        var processor = new PipelineInvokingMessageProcessor(new FakeBaseReceiver(), fakeLogger, headerExtractor);
        MessageContext? capturedMessageContext = null;
        ErrorContext? capturedErrorContext = null;
        var messageActions = new TestableMessageActions();

        await processor.Initialize(PushRuntimeSettings.Default,
            async (msgContext, token) =>
            {
                capturedMessageContext = msgContext;
                await onMessage(msgContext, token);
            },
            async (errorContext, token) =>
            {
                capturedErrorContext = errorContext;
                return await onError(errorContext, token);
            },
            cancellationToken);

        Assert.DoesNotThrowAsync(async () => await processor.Process(message, messageActions, cancellationToken));

        return new ProcessingResult(messageActions, capturedMessageContext, capturedErrorContext, fakeLogger.Collector);
    }

    record ProcessingResult(TestableMessageActions MessageActions, MessageContext? MessageContext, ErrorContext? ErrorContext, FakeLogCollector LogCollector)
    {
        public bool OnMessageWasCalled => MessageContext != null;
        public bool OnErrorWasCalled => ErrorContext != null;
    }

    class TestableMessageActions : ServiceBusMessageActions
    {
        public bool WasCompleted { get; private set; }
        public bool WasAbandoned { get; private set; }
        public bool WasDeadLettered => DeadLetterDetails is not null;
        public DeadLetterCallDetails? DeadLetterDetails { get; private set; }

        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = new CancellationToken())
        {
            WasCompleted = true;
            return Task.CompletedTask;
        }

        public override Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = new CancellationToken())
        {
            WasAbandoned = true;
            return Task.CompletedTask;
        }

        public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, Dictionary<string, object>? propertiesToModify = null, string? deadLetterReason = null, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = new CancellationToken())
        {
            DeadLetterDetails = new(deadLetterReason, deadLetterErrorDescription, propertiesToModify);
            return Task.CompletedTask;
        }

        public record DeadLetterCallDetails(string? DeadLetterReason, string? DeadLetterErrorDescription, Dictionary<string, object>? DeadLetterProperties);
    }

    class FakeBaseReceiver : IMessageReceiver
    {
        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = new()) => Task.CompletedTask;
        public Task StartReceive(CancellationToken cancellationToken = new()) => throw new NotImplementedException();
        public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = new()) => throw new NotImplementedException();
        public Task StopReceive(CancellationToken cancellationToken = new()) => throw new NotImplementedException();
        public ISubscriptionManager Subscriptions => null!;
        public string Id => string.Empty;
        public string ReceiveAddress => "TestEndpoint";
    }
}