namespace NServiceBus;

public record SendOnlyManifest(string Name, Action<EndpointConfiguration> EndpointConfiguration);