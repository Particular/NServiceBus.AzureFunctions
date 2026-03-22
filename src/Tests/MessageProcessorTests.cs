namespace NServiceBus.AzureFunctions.Tests;

using Azure.Messaging.ServiceBus;
using AzureServiceBus.Serverless.TransportWrapper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
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

        var message = ServiceBusModelFactory.ServiceBusReceivedMessage(
            messageId: expectedMessageId,
            properties: new Dictionary<string, object> { { expectedHeaderKey, expectedHeaderValue } },
            body: new BinaryData(expectedBody),
            replyTo: expectedReplyTo,
            correlationId: expectedCorrelationId
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
            Assert.IsTrue(messageContext.Headers.ContainsKey(Headers.CorrelationId), "Native CorrelationId should be upconverted to a the CorrelationId header");
            Assert.AreEqual(expectedCorrelationId, messageContext.Headers[Headers.CorrelationId], "Headers should expose the correct CorrelationId value");
            Assert.IsTrue(messageContext.Headers.ContainsKey(Headers.ReplyToAddress), "Native ReplyTo should be upconverted to a the ReplyToAddress header");
            Assert.AreEqual(expectedReplyTo, messageContext.Headers[Headers.ReplyToAddress], "Headers should expose the correct ReplyTo header value");
        }
    }

    [Test]
    public async Task Should_require_native_message_id_to_be_set()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();

        var result = await ProcessMessage(
            message: message
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.False(result.OnMessageWasCalled, "OnMessage should not be called");
            Assert.False(result.OnErrorWasCalled, "OnError should not be called");
            Assert.True(result.MessageActions.WasDeadLettered, "Missing native message id should result in message being dead lettered");
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
        }
    }

    //TODO: Tests to add
    // ShouldDLQHeaderExtractionFails?
    // ShouldNotAllowHeaderOrBodyMutationsAcrossOnMessageAndOnError
    // Should_expose_a_unique_transport_transaction_for_onmessage_and_onerror?

    async Task<ProcessingResult> ProcessMessage(
        ServiceBusReceivedMessage? message = null,
        Func<MessageContext, CancellationToken, Task>? onMessage = null,
        Func<ErrorContext, CancellationToken, Task<ErrorHandleResult>>? onError = null,
        CancellationToken cancellationToken = default)
    {
        message ??= ServiceBusModelFactory.ServiceBusReceivedMessage(messageId: Guid.NewGuid().ToString());
        onMessage ??= (_, _) => Task.CompletedTask;
        onError ??= (_, _) => Task.FromResult(ErrorHandleResult.RetryRequired);

        var processor = new PipelineInvokingMessageProcessor(new FakeBaseReceiver(), NullLogger<PipelineInvokingMessageProcessor>.Instance);
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

        return new ProcessingResult(messageActions, capturedMessageContext, capturedErrorContext);
    }

    class ProcessingResult(TestableMessageActions messageActions, MessageContext? messageContext, ErrorContext? errorContext)
    {
        public TestableMessageActions MessageActions { get; } = messageActions;
        public MessageContext? MessageContext { get; } = messageContext;
        public ErrorContext? ErrorContext { get; } = errorContext;
        public bool OnMessageWasCalled => MessageContext != null;
        public bool OnErrorWasCalled => ErrorContext != null;
    }

    class TestableMessageActions : ServiceBusMessageActions
    {
        public bool WasCompleted { get; private set; }
        public bool WasAbandoned { get; private set; }
        public bool WasDeadLettered { get; private set; }

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
            WasDeadLettered = true;
            return Task.CompletedTask;
        }
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