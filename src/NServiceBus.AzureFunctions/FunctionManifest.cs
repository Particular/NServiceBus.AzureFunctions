namespace NServiceBus;

using Microsoft.Azure.Functions.Worker.Builder;

public delegate void FunctionManifestRegistration(FunctionsApplicationBuilder builder, FunctionManifest functionManifest);

public sealed record FunctionManifest(
    string Name,
    string Address,
    string ConnectionSettingName,
    FunctionEndpointConfiguration Configuration,
    FunctionManifestRegistration Register);
