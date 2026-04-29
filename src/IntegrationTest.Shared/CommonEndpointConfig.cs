namespace IntegrationTest.Shared;

using Infrastructure;

public static class CommonEndpointConfig
{
    public static void Apply(EndpointConfiguration configuration)
    {
        var hostId = Environment.GetEnvironmentVariable(
            "AzureFunctionsWebHost__hostid");

        var hostInstanceId =
            Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");

        Console.WriteLine($"Host ID: {hostId},  Instance Id: {hostInstanceId}");
        configuration.UseTransport(new AzureServiceBusServerlessTransport(TopicTopology.Default));
        configuration.EnableInstallers();
        configuration.UsePersistence<LearningPersistence>();
        configuration.UseSerialization<SystemJsonSerializer>();

        configuration.EnableFeature<TestStorageFeature>();
    }
}