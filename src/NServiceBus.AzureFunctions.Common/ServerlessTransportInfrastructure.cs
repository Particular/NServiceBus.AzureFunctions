namespace NServiceBus;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

/// <summary>
/// A <see cref="TransportInfrastructure"/> wrapper that replaces the base transport's receivers so
/// the NServiceBus pipeline processes messages delivered by an Azure Functions trigger rather than
/// a transport-owned message pump.
/// </summary>
public sealed class ServerlessTransportInfrastructure : TransportInfrastructure
{
    readonly TransportInfrastructure baseTransportInfrastructure;

    /// <summary>
    /// Wraps <paramref name="baseTransportInfrastructure"/>, replacing its receivers with ones
    /// produced by <paramref name="messageReceiverFactory"/>.
    /// </summary>
    /// <param name="baseTransportInfrastructure">The underlying transport infrastructure to wrap.</param>
    /// <param name="messageReceiverFactory">Factory that wraps each base receiver with a serverless equivalent driven by the Functions trigger.</param>
    public ServerlessTransportInfrastructure(
        TransportInfrastructure baseTransportInfrastructure,
        Func<IMessageReceiver, IMessageReceiver> messageReceiverFactory)
    {
        this.baseTransportInfrastructure = baseTransportInfrastructure;
        Dispatcher = baseTransportInfrastructure.Dispatcher;
        Receivers = baseTransportInfrastructure.Receivers.ToDictionary(r => r.Key, r => messageReceiverFactory(r.Value));
    }

    /// <inheritdoc />
    public override Task Shutdown(CancellationToken cancellationToken = default) => baseTransportInfrastructure.Shutdown(cancellationToken);

    /// <inheritdoc />
    public override string ToTransportAddress(QueueAddress address) => baseTransportInfrastructure.ToTransportAddress(address);
}