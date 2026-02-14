namespace NServiceBus;

public record FunctionManifest(string Name, string Queue, string ConnectionName, IEndpointConfiguration EndpointConfiguration);