namespace NServiceBus.MultiHosting;

/// <summary>
/// Wraps IMessageSession to provide endpoint-aware message session functionality in multi-endpoint scenarios.
/// Ensures the endpoint is started before any messaging operation is performed.
/// </summary>
public class HostAwareMessageSession(IEndpointStarter endpointStarter) : IMessageSession
{
    public async Task Send(object message, SendOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Send(message, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task Send<T>(Action<T> messageConstructor, SendOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Send(messageConstructor, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task Publish(object message, PublishOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Publish(message, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task Publish<T>(Action<T> messageConstructor, PublishOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Publish(messageConstructor, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task Subscribe(Type eventType, SubscribeOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Subscribe(eventType, options, cancellationToken).ConfigureAwait(false);
    }

    public async Task Unsubscribe(Type eventType, UnsubscribeOptions options, CancellationToken cancellationToken = default)
    {
        var messageSession = await endpointStarter.GetOrStart(cancellationToken).ConfigureAwait(false);
        await messageSession.Unsubscribe(eventType, options, cancellationToken).ConfigureAwait(false);
    }
}
