namespace NServiceBus;

record FunctionManifest(string Name, string Queue, string ConnectionName)
{
    public bool Configured { get; set; }
}