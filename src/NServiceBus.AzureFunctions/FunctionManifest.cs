namespace NServiceBus;

public sealed record FunctionManifest(string Name, string Address, string ConnectionSettingName, FunctionEndpointConfiguration Configuration);
