namespace NServiceBus.AzureFunctions.Tests;

using NUnit.Framework;
using Transport.AzureServiceBus;

[TestFixture]
public class AzureServiceBusServerlessTransportTests
{
    [Test]
    public void Should_pass_through_enable_partitioning()
    {
        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { EnablePartitioning = true };

        Assert.That(transport.innerTransport.EnablePartitioning, Is.True);
    }

    [Test]
    public void Should_pass_through_entity_maximum_size()
    {
        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { EntityMaximumSize = 8 };

        Assert.That(transport.innerTransport.EntityMaximumSize, Is.EqualTo(8));
    }

    [Test]
    public void Should_pass_through_hierarchy_namespace_options()
    {
        var hierarchyNamespaceOptions = new HierarchyNamespaceOptions { HierarchyNamespace = "sales" };

        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { HierarchyNamespaceOptions = hierarchyNamespaceOptions };

        Assert.That(transport.innerTransport.HierarchyNamespaceOptions, Is.SameAs(hierarchyNamespaceOptions));
    }

    [Test]
    public void Should_pass_through_auto_forward_dlq()
    {
        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default) { AutoForwardDeadLetteredMessagesToErrorQueue = false };

        Assert.That(transport.innerTransport.AutoForwardDeadLetteredMessagesToErrorQueue, Is.False);
    }

    [Test]
    public void Should_default_auto_forward_dlq_to_true()
    {
        var transport = new AzureServiceBusServerlessTransport(TopicTopology.Default);

        Assert.That(transport.innerTransport.AutoForwardDeadLetteredMessagesToErrorQueue, Is.True);
    }
}