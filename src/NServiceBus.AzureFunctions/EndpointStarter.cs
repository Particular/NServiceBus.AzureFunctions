namespace NServiceBus.AzureFunctions;

using NServiceBus.MultiHosting;
using NServiceBus.MultiHosting.Services;

public class EndpointStarter(
    IStartableEndpointWithExternallyManagedContainer startableEndpoint,
    IServiceProvider serviceProvider,
    ServerlessTransport serverlessTransport,
    string serviceKey,
    KeyedServiceCollectionAdapter services) : IEndpointStarter
{
    public string ServiceKey => serviceKey;

    public async ValueTask<IEndpointInstance> GetOrStart(CancellationToken cancellationToken = default)
    {
        if (endpoint != null)
        {
            return endpoint;
        }

        await startSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (endpoint != null)
            {
                return endpoint;
            }

            keyedServices = new KeyedServiceProviderAdapter(serviceProvider, serviceKey, services);
            serverlessTransport.ServiceProvider = keyedServices;

            using var scope = FunctionsLoggerFactory.Instance.PushName(ServiceKey);
            scope.Flush();

            endpoint = await startableEndpoint.Start(keyedServices, cancellationToken).ConfigureAwait(false);

            return endpoint;
        }
        finally
        {
            startSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (endpoint == null || keyedServices == null)
        {
            return;
        }

        using var scope = FunctionsLoggerFactory.Instance.PushName(ServiceKey);
        if (endpoint != null)
        {
            await endpoint.Stop().ConfigureAwait(false);
        }

        if (keyedServices != null)
        {
            await keyedServices.DisposeAsync().ConfigureAwait(false);
        }
        startSemaphore.Dispose();
    }

    readonly SemaphoreSlim startSemaphore = new(1, 1);

    IEndpointInstance? endpoint;
    KeyedServiceProviderAdapter? keyedServices;
}