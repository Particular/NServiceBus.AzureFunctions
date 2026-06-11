namespace NServiceBus.AzureFunctions.Tests;

using Extensibility;
using IntegrationTest.Contracts;
using IntegrationTest.Shared.Infrastructure;
using NUnit.Framework;
using Testing;

[TestFixture]
public class TestStorageInfrastructureTests
{
    [Test]
    public async Task Global_storage_should_keep_all_concurrent_messages()
    {
        var storage = new GlobalTestStorage();
        const string testName = "concurrent-test";
        const int messageCount = 1_000;

        var tasks = Enumerable.Range(0, messageCount)
            .Select(i => Task.Run(() => storage.Add(testName, new MessageReceived($"Message{i}", i, "sender", "receiver"))));

        await Task.WhenAll(tasks);

        var payload = storage.CreatePayload(testName);

        Assert.That(payload.MessagesReceived, Has.Length.EqualTo(messageCount));
        Assert.That(payload.MessagesReceived.Select(m => m.Order), Is.EquivalentTo(Enumerable.Range(0, messageCount)));
    }

    [Test]
    public async Task Outgoing_behavior_should_increment_order_across_sibling_outgoing_contexts()
    {
        var parentExtensions = new ContextBag();
        parentExtensions.Set(new TestStorageContext("order-test", 5));

        var behavior = new OutgoingTestBehavior();
        var firstOutgoingMessage = new TestableOutgoingPhysicalMessageContext
        {
            Extensions = new ContextBag(parentExtensions)
        };
        var secondOutgoingMessage = new TestableOutgoingPhysicalMessageContext
        {
            Extensions = new ContextBag(parentExtensions)
        };

        await behavior.Invoke(firstOutgoingMessage, _ => Task.CompletedTask);
        await behavior.Invoke(secondOutgoingMessage, _ => Task.CompletedTask);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstOutgoingMessage.Headers["TestCaseName"], Is.EqualTo("order-test"));
            Assert.That(secondOutgoingMessage.Headers["TestCaseName"], Is.EqualTo("order-test"));
            Assert.That(firstOutgoingMessage.Headers["TestStorageOrder"], Is.EqualTo("6"));
            Assert.That(secondOutgoingMessage.Headers["TestStorageOrder"], Is.EqualTo("7"));
        }
    }

    [Test]
    public async Task Outgoing_behavior_should_assign_initial_order_when_no_incoming_context_exists()
    {
        var behavior = new OutgoingTestBehavior();
        var outgoingMessage = new TestableOutgoingPhysicalMessageContext
        {
            Headers =
            {
                ["TestCaseName"] = "send-only-test"
            }
        };

        await behavior.Invoke(outgoingMessage, _ => Task.CompletedTask);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(outgoingMessage.Headers["TestCaseName"], Is.EqualTo("send-only-test"));
            Assert.That(outgoingMessage.Headers["TestStorageOrder"], Is.EqualTo("1"));
        }
    }
}
