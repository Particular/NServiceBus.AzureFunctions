namespace NServiceBus;

public sealed record FunctionManifest(string Name, string Queue, string ConnectionName, FunctionEndpointConfiguration Configuration);