namespace NServiceBus;

using System;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Transport;

public abstract class ServerlessTransport(
    TransportTransactionMode defaultTransactionMode,
    bool supportsDelayedDelivery,
    bool supportsPublishSubscribe,
    bool supportsTtbr)
    : TransportDefinition(defaultTransactionMode, supportsDelayedDelivery, supportsPublishSubscribe, supportsTtbr)
{
    public IServiceProvider? ServiceProvider { get; set; }

    public virtual void RegisterServices(IServiceCollection services, string endpointName)
    {
    }
}