namespace NServiceBus.AzureFunctions.Tests;

using Azure.Messaging.ServiceBus;
using AzureServiceBus.Serverless.TransportWrapper;
using Microsoft.Azure.Functions.Worker;
using NUnit.Framework;
using Transport;

[TestFixture]
public class MessageProcessorTests
{
    [Test]
    public async Task Should_complete_when_on_message_succeeds()
    {
        var result = await ProcessMessage(
            onMessage: _ => Task.CompletedTask);

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled);
            Assert.IsFalse(result.OnErrorWasCalled);
            Assert.IsTrue(result.MessageActions.WasCompleted);
            Assert.IsFalse(result.MessageActions.WasAbandoned);
        }
    }

    [Test]
    public async Task Should_abandon_when_on_message_fails_and_retry_is_requested()
    {
        var result = await ProcessMessage(
            onMessage: _ => throw new Exception("simulated exception"));

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled);
            Assert.IsTrue(result.OnErrorWasCalled);
            Assert.IsFalse(result.MessageActions.WasCompleted);
            Assert.IsTrue(result.MessageActions.WasAbandoned);
        }
    }

    [Test]
    public async Task Should_complete_when_on_message_fails_and_failure_is_marked_as_handled()
    {
        var result = await ProcessMessage(
            onMessage: _ => throw new Exception("simulated exception"),
            onError: _ => Task.FromResult(ErrorHandleResult.Handled));

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(result.OnMessageWasCalled);
            Assert.IsTrue(result.OnErrorWasCalled);
            Assert.IsTrue(result.MessageActions.WasCompleted);
            Assert.IsFalse(result.MessageActions.WasAbandoned);
        }
    }

    [Test]
    public async Task Should_expose_servicebus_message_on_both_message_and_error_context()
    {
        var message = ServiceBusModelFactory.ServiceBusReceivedMessage();

        var result = await ProcessMessage(
            message: message,
            onMessage: _ => throw new Exception("simulated exception"));

        using (Assert.EnterMultipleScope())
        {
            Assert.AreSame(message, result.MessageContext?.Extensions.Get<ServiceBusReceivedMessage>());
            Assert.AreSame(message, result.ErrorContext?.Extensions.Get<ServiceBusReceivedMessage>());
        }
    }

    //TODO: Tests to add
    // ShouldSupportLegacyWcfBody ?
    // ShouldDefaultMessageIdToNewGuid
    // ShouldNotInvokeOnErrorIfCancellationIsRequested
    // ShouldDLQMessageIfBodyOrHeaderExtractionFails?
    // ShouldNotAllowHeaderOrBodyMutationsAcrossOnMessageAndOnError

#pragma warning disable CS8425 // Func used as a method parameter with a Task return type argument should have at least one CancellationToken parameter type argument
#pragma warning disable PS0013
    async Task<ProcessingResult> ProcessMessage(
        ServiceBusReceivedMessage? message = null,
        Func<MessageContext, Task>? onMessage = null,
        Func<ErrorContext, Task<ErrorHandleResult>>? onError = null)
    {
        message ??= ServiceBusModelFactory.ServiceBusReceivedMessage();
        onMessage ??= _ => Task.CompletedTask;
        onError ??= _ => Task.FromResult(ErrorHandleResult.RetryRequired);

        var processor = new PipelineInvokingMessageProcessor(new FakeBaseReceiver());
        MessageContext? capturedMessageContext = null;
        ErrorContext? capturedErrorContext = null;
        var messageActions = new TestableMessageActions();

        await processor.Initialize(PushRuntimeSettings.Default,
            async (msgContext, _) =>
            {
                capturedMessageContext = msgContext;
                await onMessage(msgContext);
            },
            async (errorContext, _) =>
            {
                capturedErrorContext = errorContext;
                return await onError(errorContext);
            });

        await processor.Process(message, messageActions);
        return new ProcessingResult(messageActions, capturedMessageContext, capturedErrorContext);
    }
#pragma warning restore PS0013
#pragma warning restore CS8425

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