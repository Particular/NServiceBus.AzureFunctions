namespace NServiceBus;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport;

public class ServerlessTransportInfrastructure : TransportInfrastructure
{
    readonly TransportInfrastructure baseTransportInfrastructure;

    public ServerlessTransportInfrastructure(
        TransportInfrastructure baseTransportInfrastructure,
        Func<IMessageReceiver, IMessageReceiver> messageReceiverFactory)
    {
        this.baseTransportInfrastructure = baseTransportInfrastructure;
        Dispatcher = baseTransportInfrastructure.Dispatcher;
        Receivers = baseTransportInfrastructure.Receivers.ToDictionary(
            r => r.Key,
            r => messageReceiverFactory(r.Value));
    }

    public override Task Shutdown(CancellationToken cancellationToken = default)
        => baseTransportInfrastructure.Shutdown(cancellationToken);

    public override string ToTransportAddress(QueueAddress address)
        => baseTransportInfrastructure.ToTransportAddress(address);
}
