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
    public async Task ShouldCallCompleteWhenOnMessageSucceeds()
    {
        var processor = new PipelineInvokingMessageProcessor(new FakeBaseReceiver());

        var onErrorWasCalled = false;
        var onMessageWasCalled = false;
        await processor.Initialize(PushRuntimeSettings.Default,
            (_, _) =>
            {
                onMessageWasCalled = true;
                return Task.CompletedTask;
            }, (_, _) =>
            {
                onErrorWasCalled = true;
                return Task.FromResult(ErrorHandleResult.Handled);
            });

        var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
        var messageActions = new TestableMessageActions();

        await processor.Process(serviceBusReceivedMessage, messageActions);

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(onMessageWasCalled);
            Assert.IsFalse(onErrorWasCalled);
            Assert.IsTrue(messageActions.WasCompleted);
            Assert.IsFalse(messageActions.WasAbandoned);
        }
    }

    [Test]
    public async Task ShouldCallAbandonWhenOnMessageFailsAndRetryIsRequested()
    {
        var processor = new PipelineInvokingMessageProcessor(new FakeBaseReceiver());

        var onErrorWasCalled = false;
        var onMessageWasCalled = false;
        await processor.Initialize(PushRuntimeSettings.Default,
            (_, _) =>
            {
                onMessageWasCalled = true;
                throw new Exception("simulated exception");
            }, (_, _) =>
            {
                onErrorWasCalled = true;
                return Task.FromResult(ErrorHandleResult.RetryRequired);
            });

        var serviceBusReceivedMessage = ServiceBusModelFactory.ServiceBusReceivedMessage();
        var messageActions = new TestableMessageActions();

        await processor.Process(serviceBusReceivedMessage, messageActions);

        using (Assert.EnterMultipleScope())
        {
            Assert.IsTrue(onMessageWasCalled);
            Assert.IsTrue(onErrorWasCalled);
            Assert.IsFalse(messageActions.WasCompleted);
            Assert.IsTrue(messageActions.WasAbandoned);
        }
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
        public Task Initialize(PushRuntimeSettings limitations, OnMessage onMessage, OnError onError, CancellationToken cancellationToken = new CancellationToken()) => Task.CompletedTask;

        public Task StartReceive(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task ChangeConcurrency(PushRuntimeSettings limitations, CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public Task StopReceive(CancellationToken cancellationToken = new CancellationToken()) => throw new NotImplementedException();

        public ISubscriptionManager Subscriptions { get; }
        public string Id { get; }
        public string ReceiveAddress { get; } = "TestEndpoint";
    }
}