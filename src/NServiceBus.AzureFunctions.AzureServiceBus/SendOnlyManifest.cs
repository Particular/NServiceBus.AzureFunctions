namespace NServiceBus;

public record SendOnlyManifest(string Name, IEndpointConfiguration EndpointConfiguration);