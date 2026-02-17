namespace IntegrationTest.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;

public class TestStorageFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();
        context.Services.AddSingleton(provider => new TestStorage(endpointName, provider.GetRequiredService<GlobalTestStorage>()));

        context.Pipeline.Register(typeof(IncomingTestBehavior), "Log received messages");
        context.Pipeline.Register(typeof(OutgoingTestBehavior), "Forward test case name to outgoing messages");
    }
}