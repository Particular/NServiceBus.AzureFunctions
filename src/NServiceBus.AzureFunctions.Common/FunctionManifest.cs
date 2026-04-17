namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

public sealed record FunctionManifest(string Name, string Address, string ConnectionSettingName, FunctionEndpointConfiguration Configuration, Action<FunctionsApplicationBuilder, FunctionManifest> Register);
