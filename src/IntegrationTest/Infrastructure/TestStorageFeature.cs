namespace IntegrationTest.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Features;

public class TestStorageFeature : Feature
{
    protected override void Setup(FeatureConfigurationContext context)
    {
        var endpointName = context.Settings.EndpointName();
        context.Services.AddSingleton(provider => new TestStorage(endpointName, provider.GetRequiredService<GlobalTestStorage>()));
        context.Services.AddSingleton<IncomingTestBehavior>();
        context.Services.AddSingleton<OutgoingTestBehavior>();

        context.Pipeline.Register("IncomingTestBehavior", p => p.GetRequiredService<IncomingTestBehavior>(), "Log received messages");
        context.Pipeline.Register("OutgoingTestBehavior", p => p.GetRequiredService<OutgoingTestBehavior>(), "Forward test case name to outgoing messages");
    }
}